<img src="src/ArcDrawAssets/ArcDraw.png" width="76" alt="ArcDraw icon">

# ArcDraw

**Windows 화면 필기와 독립적인 화이트보드 노트를 함께 사용하는 앱입니다.**
ppInk/gInk를 바탕으로 노트별 텍스트, CAPS 입력 전환, 툴바 테마 등을 추가했습니다.

**[Windows 실행 ZIP 다운로드](https://github.com/gamzagui-collab/ArcDraw/releases/latest/download/ArcDraw-v0.17-windows-x86.zip)** · [릴리스 안내](https://github.com/gamzagui-collab/ArcDraw/releases)

## 실행 방법

1. 위 실행 ZIP을 내려받아 새 폴더에 **모두 압축 해제**합니다.
2. 같은 폴더의 **ArcDraw.exe**를 실행합니다. EXE 이름은 ArcDraw에서도 그대로 사용합니다.
3. 화면의 ArcDraw 아이콘을 누르고 **Caps Lock을 켜면** 그릴 수 있습니다.

필요 환경은 Windows 데스크톱과 .NET Framework 4.8입니다. 기존 ppInk 설치 폴더와 분리해서 실행하세요. 시스템 DLL을 교체할 필요는 없습니다.

## 주요 기능

- 화면 펜·지우개와 별개로 사용하는 여러 화이트보드 노트
- 노트별 한글·영문·여러 줄 텍스트 입력 및 수정
- 노트 이동, 모서리 크기 조절, 개별 숨김·표시·삭제
- 선택한 화면 또는 노트에 적용되는 삭제·실행 취소·다시 실행
- 10개 툴바 테마와 가로 1줄·가로 2줄·세로 배치, 크기 조절
- 접힌 툴바에서도 CAPS 상태 표시, 그리기 상태의 얇은 화면 테두리

## 기본 조작

| 조작 | 동작 |
| --- | --- |
| Caps Lock ON / OFF | 그리기·툴바 펼치기 / 일반 조작·자동 접기 |
| 점 6개 이동 블록 | ON/OFF 상태 모두 툴바 이동 |
| Tab (CAPS ON) | 툴바 접기·펼치기 |
| Alt 누르고 있기 | 도구별 단축키 표시 |
| Delete (CAPS ON) | 현재 선택 대상의 마지막 작업 실행 취소 |
| Ctrl+Delete (CAPS ON) | 현재 선택 대상 전체 삭제 |
| Ctrl+Shift+Z | 다시 실행 |
| 1~0 (저장한 프리셋) | 색·굵기·펜촉·화면 도구 불러오기 |
| ⚙ 설정 | 프리셋·글꼴·테마 설정 |
| T | 텍스트 도구 |
| Ctrl+Enter / Esc (노트 텍스트 편집 중) | 확정 / 취소 |
| Esc (그리기 중) | 노트를 숨기고 아이콘으로 복귀; 다시 켜면 복원 |

화면 또는 노트를 선택한 뒤 삭제·실행 취소를 사용하세요. 노트 텍스트를 수정할 때에는 T 도구로 기존 글자를 누릅니다.

## v0.17 변경과 확인 범위

- **그리기 ON 상태에서도 노트·설정 카드의 버튼을 바로 클릭**할 수 있도록 수정했습니다.
- 카드가 그리기 창에 가려져 클릭이 필기로 들어가던 문제를 수정했습니다. 그리기 창이 다시 만들어진 경우에도 카드를 현재 창에 연결합니다.
- 카드 클릭 도중 그리기 창으로 포커스를 되돌려 버튼 클릭을 취소하던 처리를 제거했습니다. 기존 클릭 시 비활성화 정책은 유지합니다.
- 필기·노트 데이터·단축키·프리셋·글꼴 설정 경로는 변경하지 않았습니다. 기존 기능은 [설정 안내](docs/SETTINGS_KO.md)를 참고하세요.

Windows Release|x86 빌드 통과. 별도 Windows 검사본이 빌드 EXE의 Root/Forms를 호스팅하여 실제 마우스로 노트 추가·설정 열기·화면/노트 선택·숨김/표시/삭제가 동작하고 카드 클릭이 필기를 만들지 않는지 확인했습니다. 카드 조작 후 일반 화면 펜과 그리기 창 재생성 후 카드 연결도 확인했습니다. Program.Main 전체 회귀 검사는 아닙니다.

변경한 카드 클릭 시나리오만 검사했으며 전체 회귀·장시간 반복 검사는 추가하지 않았습니다. 물리 펜/터치, 게임 독점 전체화면 및 다양한 고DPI·멀티 모니터 환경은 검증 범위에 포함되지 않습니다.

## 소스 빌드

C# Windows Forms / .NET Framework 4.8 프로젝트입니다. [빌드 안내](docs/BUILD_KO.md)를 참고하세요.

## 원본·라이선스

- 원본: [ppInk — Pubpub-ZZ](https://github.com/pubpub-zz/ppInk), [gInk — Weizhi Nai](https://github.com/geovens/gInk)
- 원본의 [MIT 라이선스](license.txt)와 저작권 표기를 유지합니다.
- 원본 소스 기준 커밋: `de97909272341489f089f1ba68ccf3d26ca82a6c`
- ArcDraw 아이콘은 사용자 요청으로 생성한 이 수정본의 구분용 이미지입니다.
- 포함 라이브러리의 표기는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요. 외부 구성요소에는 각각의 라이선스가 적용됩니다.
