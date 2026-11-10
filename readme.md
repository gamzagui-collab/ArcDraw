<img src="src/ArcDrawAssets/ArcDraw.png" width="76" alt="ArcDraw icon">

# ArcDraw

**Windows 화면 필기와 독립적인 화이트보드 노트를 함께 사용하는 앱입니다.**
ppInk/gInk를 바탕으로 노트별 텍스트, CAPS 입력 전환, 툴바 테마 등을 추가했습니다.

**[Windows 실행 ZIP 다운로드](https://github.com/gamzagui-collab/ArcDraw/releases/latest/download/ArcDraw-v0.14-windows-x86.zip)** · [릴리스 안내](https://github.com/gamzagui-collab/ArcDraw/releases)

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
| Caps Lock ON / OFF | 그리기 / 일반 화면 조작 |
| Tab (CAPS ON) | 툴바 접기·펼치기 |
| Alt 누르고 있기 | 도구별 단축키 표시 |
| Shift+Delete (CAPS ON) | 현재 선택 대상 전체 삭제 |
| Ctrl+Delete (CAPS ON) | 현재 선택 대상의 마지막 작업 실행 취소 |
| Ctrl+Shift+Z | 다시 실행 |
| T | 텍스트 도구 |
| Ctrl+Enter / Esc (노트 텍스트 편집 중) | 확정 / 취소 |
| Esc (그리기 중) | 그리기 종료 후 ArcDraw 아이콘으로 복귀 |

화면 또는 노트를 선택한 뒤 삭제·실행 취소를 사용하세요. 노트 텍스트를 수정할 때에는 T 도구로 기존 글자를 누릅니다.

## v0.14 변경과 확인 범위

- 실행 파일 이름을 **ArcDraw.exe**로 변경했습니다. 설정·필기 데이터 위치는 유지합니다.
- 도형·클립아트 드래그의 자석 맞춤은 기존 획 경계를 캐시해 먼 획의 네이티브 검색을 제외합니다. 맞춤 기능은 유지합니다.
- 미리보기의 바뀐 영역만 복원·전송하고, 포인터가 멈추면 같은 프레임을 다시 만들지 않습니다. 지원되지 않는 환경은 기존 전체 갱신 경로로 처리합니다.
- 꺼진 측정 툴팁을 위한 획 길이 계산도 생략합니다.

동일한 300획 장면에서 맞춤 검색 60회는 약 1158ms → 17ms, 미리보기 18프레임 처리는 약 110ms → 43ms였습니다. 이는 고정 장면의 CPU 처리 시간 비교이며 실제 사용자 FPS를 의미하지 않습니다.

Windows Release|x86 빌드, 60개 맞춤 결과의 기존 버전 일치, 사각형·타원·클립아트 크기 상자의 좌/우 미리보기 픽셀 일치, 실제 ArcDraw.exe의 첫 펜·사각형·별 클립아트 표시를 확인했습니다.

전체 회귀·장시간 반복 검사를 추가하지 않았습니다. 게임 독점 전체화면 및 다양한 고DPI·멀티 모니터 환경은 검증 범위에 포함되지 않습니다.

## 소스 빌드

C# Windows Forms / .NET Framework 4.8 프로젝트입니다. [빌드 안내](docs/BUILD_KO.md)를 참고하세요.

## 원본·라이선스

- 원본: [ppInk — Pubpub-ZZ](https://github.com/pubpub-zz/ppInk), [gInk — Weizhi Nai](https://github.com/geovens/gInk)
- 원본의 [MIT 라이선스](license.txt)와 저작권 표기를 유지합니다.
- 원본 소스 기준 커밋: `de97909272341489f089f1ba68ccf3d26ca82a6c`
- ArcDraw 아이콘은 사용자 요청으로 생성한 이 수정본의 구분용 이미지입니다.
- 포함 라이브러리의 표기는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요. 외부 구성요소에는 각각의 라이선스가 적용됩니다.
