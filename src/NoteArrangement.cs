using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace gInk
{
    public sealed partial class WhiteboardLayerManager
    {
        internal static bool TryPlanNoteArrangement(Size[] sizes,Rectangle area,bool vertical,out Rectangle[] positions)
        {
            positions=new Rectangle[sizes.Length];const int gap=8;int x=area.Left,y=area.Top,shelf=0;
            for(int i=0;i<sizes.Length;i++)
            {
                Size size=sizes[i];if(size.Width>area.Width||size.Height>area.Height||size.Width<=0||size.Height<=0)return false;
                if(vertical)
                {
                    if(y+size.Height>area.Bottom){x+=shelf+gap;y=area.Top;shelf=0;}
                    if(x+size.Width>area.Right)return false;
                    positions[i]=new Rectangle(new Point(x,y),size);y+=size.Height+gap;shelf=Math.Max(shelf,size.Width);
                }
                else
                {
                    if(x+size.Width>area.Right){y+=shelf+gap;x=area.Left;shelf=0;}
                    if(y+size.Height>area.Bottom)return false;
                    positions[i]=new Rectangle(new Point(x,y),size);x+=size.Width+gap;shelf=Math.Max(shelf,size.Height);
                }
            }
            return true;
        }
        public void ArrangeNotes(bool vertical)
        {
            if(disposed||!open||HasBusyInk)return;
            var visible=new List<WhiteboardLayerForm>();foreach(WhiteboardLayerForm note in notes)if(!note.UserHidden&&!note.IsDisposed)visible.Add(note);
            if(visible.Count==0)return;
            Rectangle area=Screen.FromControl(root.FormCollection.gpButtons).WorkingArea;area.Inflate(-8,-8);
            var sizes=new Size[visible.Count];for(int i=0;i<visible.Count;i++)sizes[i]=visible[i].Size;
            Rectangle[] positions;
            if(!TryPlanNoteArrangement(sizes,area,vertical,out positions))
            {
                MessageBox.Show(bar,"현재 노트 크기로는 화면 안에 겹치지 않게 모두 배치할 수 없습니다.\n노트 크기를 줄이거나 일부 노트를 숨긴 뒤 다시 정렬해 주세요.\n기존 배치는 유지했습니다.","ArcDraw 노트 정렬",MessageBoxButtons.OK,MessageBoxIcon.Information);
                TextEditingEnded();return;
            }
            for(int i=0;i<visible.Count;i++){visible[i].FinishEditing(true);visible[i].Location=positions[i].Location;}
            KeepWindowsOnTop();
        }
    }
}
