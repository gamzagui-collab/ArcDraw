<img src="src/ArcDrawAssets/ArcDraw.png" width="76" alt="ArcDraw icon">

# ArcDraw

**Windows 화면 필기와 독립적인 화이트보드 노트를 함께 사용하는 앱입니다.**
ppInk/gInk를 바탕으로 노트별 텍스트, CAPS 입력 전환, 툴바 테마 등을 추가했습니다.

**[Windows 실행 ZIP 다운로드](https://github.com/gamzagui-collab/ArcDraw/releases/latest/download/ArcDraw-v0.16-windows-x86.zip)** · [릴리스 안내](https://github.com/gamzagui-collab/ArcDraw/releases)

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

## v0.16 변경과 확인 범위

- CAPS ON에서 **Delete = 마지막 작업 실행 취소**, **Ctrl+Delete = 선택 대상 전체 삭제**입니다. Shift+Delete는 ArcDraw 전체 삭제에 사용하지 않습니다. 화면/노트를 먼저 선택하세요.
- 노트 목록 위 **⚙ 설정**에 **펜 프리셋 / 글꼴 / 툴바** 탭을 모았습니다.
- 기존 숫자키(기본 1~0)에 색상·불투명도·굵기·펜촉·화면 도구·채움·선 모양을 저장하고 호출합니다. 자유펜, 직선, 사각형, 원/타원, 시작·끝 화살표, 연속선을 지원합니다. 노트는 기존 독립 펜 입력을 유지하며 도형·점선 프리셋은 화면 도구에 적용됩니다.
- 새 화면/노트 텍스트의 글꼴·크기·스타일을 고를 수 있습니다. 선택한 노트의 기존 글자에도 적용하는 옵션이 있습니다.
- **ESC로 노트를 숨긴 뒤 같은 프로세스에서 다시 그리기를 켜면 내용과 위치를 복원**합니다. 완전 종료 후 노트 자동 저장 기능을 추가한 것은 아닙니다.
- 프리셋·글꼴은 `drawing-settings.xml`에 저장하며 이전 설정을 `.bak`으로 보관합니다. 기존 펜·단축키 설정 파일을 덮어쓰지 않습니다.

Windows Release|x86 빌드 통과. 별도 Windows 검사본이 빌드 EXE의 Root/Forms를 호스팅하여 실제 마우스 필기, 일반 Delete·숫자키·ESC, 선택 대상 격리, 프리셋/글꼴 저장·재읽기, 노트 숨김/재진입을 확인했습니다. 조합키 입력 도구가 Shift를 중간에 해제하는 환경이어서 Ctrl/Shift 조합은 앱 KeyboardHook에 정확한 이벤트 순서를 재생하여 확인했습니다. **물리 키보드 조합·표준 글꼴 대화상자 수동 선택은 사용자 확인 항목**입니다. 물리 한글 IME는 이번 변경에서 재검사하지 않았습니다.

전체 회귀·장시간 반복 검사는 추가하지 않았습니다. 게임 독점 전체화면 및 다양한 고DPI·멀티 모니터 환경은 검증 범위에 포함되지 않습니다.

자세한 사용법은 [설정 안내](docs/SETTINGS_KO.md)를 참고하세요.

## 소스 빌드

C# Windows Forms / .NET Framework 4.8 프로젝트입니다. [빌드 안내](docs/BUILD_KO.md)를 참고하세요.

## 원본·라이선스

- 원본: [ppInk — Pubpub-ZZ](https://github.com/pubpub-zz/ppInk), [gInk — Weizhi Nai](https://github.com/geovens/gInk)
- 원본의 [MIT 라이선스](license.txt)와 저작권 표기를 유지합니다.
- 원본 소스 기준 커밋: `de97909272341489f089f1ba68ccf3d26ca82a6c`
- ArcDraw 아이콘은 사용자 요청으로 생성한 이 수정본의 구분용 이미지입니다.
- 포함 라이브러리의 표기는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요. 외부 구성요소에는 각각의 라이선스가 적용됩니다.
