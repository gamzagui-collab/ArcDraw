# Windows 소스 빌드

- Visual Studio의 .NET 데스크톱 개발 도구 또는 MSBuild
- .NET Framework 4.8 targeting pack
- NuGet CLI
- 기본 대상: Release|x86

저장소 전체를 받은 뒤 Developer PowerShell에서 다음을 실행합니다.

```powershell
nuget restore ppInk.sln -NonInteractive
MSBuild.exe ppInk.sln /t:Rebuild /p:Configuration=Release /p:Platform=x86 /p:PostBuildEvent=
```

출력은 `ppInk/ArcDraw.exe`입니다. 리소스와 필요한 DLL을 같은 폴더에 두고 실행하세요.
기존 개인 설치본과 다른 폴더에서 작업하세요. 빌드 출력 폴더가 원래 설치본을 가리키도록 바꾸지 마세요.

NuGet 종속성은 GameOverlay.Net 4.3.1, SharpDX/SharpDX.Direct2D1/SharpDX.DXGI 4.2.0입니다.
Microsoft.Ink와 Cyotek 참조 DLL은 `ppInk` 폴더를 사용합니다. 이 공개본은 Release|x86용 참조를 포함합니다.

이 문서는 Visual Studio/MSBuild가 준비된 PC에서의 빌드 방법입니다. 자동 설치, 관리자 작업, 시스템 DLL 교체는 수행하지 않습니다.
공유 ZIP에는 이미 검증한 실행본을 넣었으며, 공개 준비 과정에서 새로운 전체 빌드를 반복하지 않았습니다.
