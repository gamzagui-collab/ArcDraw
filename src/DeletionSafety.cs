using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    internal static class DeletionSafety
    {
        internal static bool Confirm(Root root,string title,string message)
        {
            using(var dialog=new Form {Name="ArcDrawDeleteConfirmation",Text=title,ClientSize=new Size(500,240),
                FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,
                StartPosition=FormStartPosition.CenterScreen,TopMost=true,Font=new Font("Malgun Gothic",9f)})
            {
                var label=new Label {Name="DeleteWarning",Text=message,UseMnemonic=false,Bounds=new Rectangle(20,20,460,150)};
                var cancel=new Button {Name="CancelDelete",Text="취소",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(390,190,90,34)};
                var remove=new Button {Name="ConfirmDelete",Text="삭제",DialogResult=DialogResult.OK,Bounds=new Rectangle(288,190,90,34)};
                dialog.Controls.AddRange(new Control[]{label,remove,cancel});dialog.AcceptButton=cancel;dialog.CancelButton=cancel;
                dialog.Shown+=delegate {cancel.Focus();};
                try {return dialog.ShowDialog(root.FormCollection)==DialogResult.OK;}
                finally {if(root.Whiteboards!=null)root.Whiteboards.TextEditingEnded();}
            }
        }
    }

    public partial class FormCollection
    {
        private readonly Dictionary<int,Stroke> pendingTextErases=new Dictionary<int,Stroke>();
        private bool screenDeleteApproved,screenDeletePromptOpen;
        private bool HasScreenText()
        {
            foreach(Stroke stroke in IC.Ink.Strokes)if(stroke.ExtendedProperties.Contains(Root.TEXT_GUID))return true;
            return false;
        }
        private bool ConfirmScreenTextClear()
        {
            if(!HasScreenText())return true;
            if(screenDeletePromptOpen)return false;
            screenDeletePromptOpen=true;
            try {return DeletionSafety.Confirm(Root,"화면 텍스트 삭제 확인","화면의 텍스트와 필기를 모두 삭제할까요?\r\n취소하면 현재 내용을 유지합니다.");}
            finally {screenDeletePromptOpen=false;}
        }
        private void DeleteScreenContents()
        {
            screenDeleteApproved=true;
            try {IC.Ink.DeleteStrokes();}
            finally {screenDeleteApproved=false;}
        }
        private bool DeferScreenTextErase(Stroke stroke)
        {
            if(screenDeleteApproved||stroke.Deleted||!stroke.ExtendedProperties.Contains(Root.TEXT_GUID))return false;
            pendingTextErases[stroke.Id]=stroke;return true;
        }
        private bool DeferSelectedScreenTextErase(Strokes strokes)
        {
            bool text=false;foreach(Stroke stroke in strokes)if(stroke.ExtendedProperties.Contains(Root.TEXT_GUID)){text=true;break;}
            if(!text||screenDeleteApproved)return false;
            foreach(Stroke stroke in strokes)pendingTextErases[stroke.Id]=stroke;
            return true;
        }
        private void ScreenStrokesDeleting(object sender,InkOverlayStrokesDeletingEventArgs e)
        {
            var protectedStrokes=new List<Stroke>();
            foreach(Stroke stroke in e.StrokesToDelete)if(DeferScreenTextErase(stroke))protectedStrokes.Add(stroke);
            // This event exposes a mutable deletion set, not a Cancel flag.
            // Remove protected text from that set and ask only after Ink is idle.
            foreach(Stroke stroke in protectedStrokes)e.StrokesToDelete.Remove(stroke);
        }
        private void ProcessScreenTextErases()
        {
            if(pendingTextErases.Count==0||screenDeletePromptOpen)return;
            if(Root.DrawingSession==null||!Root.DrawingSession.Running){pendingTextErases.Clear();return;}
            if(ScreenContentBusy||Root.FingerInAction||IC.CollectingInk||(GetAsyncKeyState(1)&0x8000)!=0||(GetAsyncKeyState(2)&0x8000)!=0||Root.DrawingSession.Editing)return;
            var strokes=new List<Stroke>();foreach(Stroke stroke in pendingTextErases.Values)if(!stroke.Deleted)strokes.Add(stroke);
            pendingTextErases.Clear();if(strokes.Count==0)return;
            screenDeletePromptOpen=true;
            try
            {
                if(!DeletionSafety.Confirm(Root,"화면 텍스트 삭제 확인","지우개로 선택한 텍스트를 삭제할까요?\r\n취소하면 텍스트를 유지합니다."))return;
                screenDeleteApproved=true;
                try {foreach(Stroke stroke in strokes)if(!stroke.Deleted)IC.Ink.DeleteStroke(stroke);}
                finally {screenDeleteApproved=false;}
                SaveUndoStrokes();Root.UponAllDrawingUpdate=true;
            }
            finally {screenDeletePromptOpen=false;}
        }
    }
}
