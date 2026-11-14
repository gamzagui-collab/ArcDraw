<img src="src/ArcDrawAssets/ArcDraw.png" width="76" alt="ArcDraw icon">

# ArcDraw

**Windows 화면 필기와 독립적인 화이트보드 노트를 함께 사용하는 앱입니다.**
ppInk/gInk를 바탕으로 노트별 텍스트, CAPS 입력 전환, 툴바 테마 등을 추가했습니다.

**[Windows 실행 ZIP 다운로드](https://github.com/gamzagui-collab/ArcDraw/releases/latest/download/ArcDraw-v0.63-windows-x86.zip)** · [릴리스 안내](https://github.com/gamzagui-collab/ArcDraw/releases)

## 실행 방법

1. 위 실행 ZIP을 내려받아 새 폴더에 **모두 압축 해제**합니다.
2. 같은 폴더의 **ArcDraw.exe**를 실행합니다. EXE 이름은 ArcDraw에서도 그대로 사용합니다.
3. 화면의 ArcDraw 아이콘을 누르고 **Caps Lock을 켜면** 그릴 수 있습니다.

필요 환경은 Windows 데스크톱과 .NET Framework 4.8입니다. 기존 ppInk 설치 폴더와 분리해서 실행하세요. 시스템 DLL을 교체할 필요는 없습니다.

## 주요 기능

- 화면 펜·지우개와 별개로 사용하는 여러 화이트보드 노트
- 노트별 한글·영문 메모장 입력·이름 변경·글꼴 설정과 자동저장/재시작 복원
- 노트 이동, 모서리 크기 조절, 개별 숨김·표시·삭제·세로/가로 정렬
- 화면과 노트에서 사용하는 도형·선 모양 프리셋
- 선택한 화면 또는 노트에 적용되는 삭제·실행 취소·다시 실행
- 10개 툴바 테마와 가로 1줄·가로 2줄·세로 배치, 크기 조절
- 접힌 툴바에서도 CAPS 상태 표시, 필기 대상에 따라 옮겨지는 12px 네온 테두리

## 기본 조작

| 조작 | 동작 |
| --- | --- |
| Caps Lock ON / OFF | 그리기·툴바 펼치기·노트 복원 / 일반 조작·자동 접기·모든 노트 숨김 |
| 점 6개 이동 블록 | ON/OFF 상태 모두 툴바 이동 |
| Tab (CAPS ON) | 툴바 접기·펼치기 |
| Alt 누르고 있기 | 도구별 단축키 표시 |
| Alt+Tab (CAPS ON) | 화면·노트 순환, Alt 놓아 확정, Esc 취소 |
| Delete (CAPS ON) | 노트 필기 / 화면 작업 실행 취소 |
| Ctrl+Delete (CAPS ON) | 노트 필기만 전체 지우기 / 화면 전체 지우기(텍스트 확인) |
| Ctrl+Shift+Z | 다시 실행 |
| 1~0 (저장한 프리셋) | 색·굵기·펜촉·도구 불러오기 |
| 노트 이름 / 옆 ●·□ / × | 선택 / 표시·숨김 / 해당 노트 삭제 |
| 세로 정렬 / 가로 정렬 | 표시 중인 노트 겹침 없이 정렬 |
| ⚙ 설정 | 프리셋·글꼴·테마 설정 |
| T | 텍스트 도구 |
| Enter (노트 메모) | 다음 줄 |
| Ctrl+Enter (노트 메모) | 내용 유지 후 편집 마침 |
| Esc (노트 메모) | 내용 유지 후 앱 아이콘 복귀 |
| Esc (CAPS OFF에서 다른 앱 사용 중) | 해당 앱에 Esc 전달 |
| Esc (그리기 중) | 노트를 숨기고 아이콘으로 복귀; 다시 켜면 복원 |

화면 또는 노트를 선택한 뒤 삭제·실행 취소를 사용하세요. 노트를 선택한 뒤 T로 상단부터 메모합니다. 기존 배치 글자는 제목 우클릭에서 편집하며 제목 옆 ✎로 이름과 해당 노트 메모의 글꼴·크기·색상을 바꿉니다.

## v0.63 브러시와 직선 방향 고정

화면이나 노트에서 브러시 또는 직선 도구를 선택하세요. Shift를 누른 채 그리기 시작하면 가로·세로로, Ctrl을 누르면 45도 대각선으로 고정됩니다.

Windows Release|x86 빌드와 실제 마우스 입력으로 화면·노트의 브러시와 직선 방향 고정 및 일반 자유펜을 확인했습니다.

자세한 사용법은 [설정 안내](docs/SETTINGS_KO.md)를 참고하세요.

## 소스 빌드

C# Windows Forms / .NET Framework 4.8 프로젝트입니다. [빌드 안내](docs/BUILD_KO.md)를 참고하세요.

## 원본·라이선스

- 원본: [ppInk — Pubpub-ZZ](https://github.com/pubpub-zz/ppInk), [gInk — Weizhi Nai](https://github.com/geovens/gInk)
- 원본의 [MIT 라이선스](license.txt)와 저작권 표기를 유지합니다.
- 원본 소스 기준 커밋: `de97909272341489f089f1ba68ccf3d26ca82a6c`
- ArcDraw 아이콘은 사용자 요청으로 생성한 이 수정본의 구분용 이미지입니다.
- 포함 라이브러리의 표기는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요. 외부 구성요소에는 각각의 라이선스가 적용됩니다.
