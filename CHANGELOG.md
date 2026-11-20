# ArcDraw 변경 사항

[최신 버전 받기](https://github.com/gamzagui-collab/ArcDraw/releases/latest) · [모든 릴리스](https://github.com/gamzagui-collab/ArcDraw/releases) · [사용설명서](docs/USER_GUIDE_KO.md) · [단축키](docs/SHORTCUTS_KO.md)

## [v0.100](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.100)

- Caps Lock 필기 모드에서 ←/→로 빠른 색상과 클립아트를 이전/다음 순서로 고르는 기능을 복원했습니다.
- 일반 설정창의 체크박스와 클립아트·돋보기·배경·텍스트·측정 항목을 겹치지 않게 정렬했습니다.

## [v0.99](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.99)

- 클립아트 관리창의 제목과 파일 표시를 ArcDraw로 정리하고, 목록과 버튼 디자인을 개선했습니다.
- 새 사용자 데이터 폴더는 `%APPDATA%\ArcDraw`입니다. 기존 설정과 필기는 첫 실행 때 복사하며 원본은 보존합니다.
- 기본 클립아트와 새 캡처·필기의 저장 위치 이름을 ArcDraw로 바꿨습니다. 기존 필기 파일도 계속 읽습니다.

## [v0.98](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.98)

- 설정창의 초기 표시 속도와 툴바 크기 조절 반응을 개선했습니다.
- 툴바와 설정 카드가 함께 이동합니다. 자물쇠는 실행할 때 잠긴 상태로 시작합니다.
- 자물쇠를 열면 화면 중앙에 큰 안내 문구가 표시됩니다. 그리기 전환은 Caps Lock으로 조작합니다.

## [v0.97](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.97)

- v0.96 실행 파일의 내부 버전이 0.95로 남아 같은 업데이트를 반복 안내하고 설치 시 버전 불일치로 거부하던 문제를 수정했습니다.

## [v0.96](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.96)

- Caps Lock을 켠 그리기 상태에서 자유펜은 ←/→로 빠른 색상을 이전·다음 순서로 고릅니다. 빠른 색상은 툴바의 색상 설정에서 1~12개로 구성할 수 있습니다.
- 클립아트 도구에서는 같은 키로 등록된 클립아트를 이전·다음 순서로 고릅니다.
- 컬러팝 SVG 액자 10종을 141~150번으로 추가했습니다. 내장 액자는 총 150종이며 그중 SVG는 140종입니다.

## [v0.95](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.95)

- Caps Lock을 켠 상태에서도 툴바 잠금 아이콘을 클릭하면 열린 상태와 닫힌 상태가 즉시 표시됩니다.
- 잠금을 연 뒤 툴바 가장자리 또는 모서리를 끌어 너비와 높이를 조절하는 입력을 수정했습니다.

## [v0.94](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.94)

- Caps Lock을 켠 그리기 상태에서 휠 버튼을 누르면 일반 클릭과 창 드래그를 할 수 있습니다. 메모장과 작업표시줄에서도 첫 누름부터 동작합니다.
- 툴바 설정의 테마를 주제별 전체 미리보기로 고를 수 있습니다. 카드에 마우스를 올리면 확대되고, 클릭하면 선택 창을 닫지 않고 바로 적용됩니다.
- 툴바의 잠금 아이콘을 열면 가장자리와 모서리를 끌어 너비·높이를 조절할 수 있습니다. 잠금을 열어 둔 동안에는 그리기가 멈춥니다.

## [v0.93](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.93)

- **내 액자 추가**에서 SVG, PNG, JPEG를 여러 개 선택할 수 있습니다. 일부 파일을 열 수 없어도 나머지는 가져옵니다.
- 4K 벡터 **설원** 액자 10종을 131~140번으로 추가했습니다. 내장 테마는 총 140종이고, 전체 인코딩은 SVG 130종을 대상으로 합니다.

## [v0.92](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.92)

- **판타지 장식** 10종(SVG)과 **귀여운 액자** 10종(원본 투명 PNG)을 추가했습니다. 내장 액자는 총 130종입니다.
- **화면 액자 → 테마 → AI 제작 프롬프트 복사하기**에서 주제, 해상도, 추가 요청사항을 입력할 수 있습니다. 해상도는 대상 화면을 기본값으로 사용하며 복사한 프롬프트에 화면 비율과 SVG viewBox가 반영됩니다.
- **전체 인코딩**은 내장 SVG 120종을 미리 변환합니다. PNG 액자는 바로 사용할 수 있습니다.

## [v0.91](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.91)

- **화면 액자 → 테두리 폭**으로 액자 바깥을 화면 가장자리에 고정한 채 안쪽으로 차지하는 폭을 조절합니다.
- 범위는 80~120%이며 100%는 원본입니다. 모서리는 가로와 세로를 같은 비율로 조정합니다.
- 폭을 조절할 때 이미 인코딩한 SVG의 PNG 캐시를 사용합니다.

## [v0.90](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.90)

- SVG 화면 액자의 첫 변환은 백그라운드에서 진행되며 선택 창에 **인코딩 중**이 표시됩니다.
- 변환한 투명 PNG는 해상도별로 보관하며 앱을 다시 켜도 사용합니다. 기존 12개 보관 제한을 없앴습니다.
- **화면 액자 → 테마 → 전체 인코딩**으로 내장 110종을 현재 표시 영역에 맞춰 준비합니다. 취소 후 다시 시작하면 저장된 항목을 건너뜁니다.

## [v0.89](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.89)

- 화면 액자 메뉴에 SVG 110종을 추가하고 클래식, 게임, 요리, 여행 등 11개 분류로 나눴습니다.
- 전체 액자는 001~110번 또는 이름으로 검색합니다. 기존 세 액자의 즐겨찾기와 최근 사용 기록을 유지합니다.
- 액자 크기를 80~100%로 조절할 수 있습니다. 가로·세로 비율과 중앙 위치를 유지하고 크기를 자동저장합니다.
- 16:9 벡터 원본을 화면 크기에 맞춰 표시합니다. 아이콘에는 Font Awesome Free (CC BY 4.0)를 사용했습니다.

## [v0.88](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.88)

- 즐겨찾기가 비어 있으면 테마 창에서 기본 액자를 먼저 보여줍니다. 최근 사용은 즐겨찾기에서 확인할 수 있습니다.
- SVG 액자 카드와 마우스를 올렸을 때의 확대 미리보기를 키우고, 얇은 선이 보이도록 배경과 그리기 방식을 바꿨습니다.

## [v0.87](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.87)

- **사이버펑크 네온** 액자 미리보기의 메모리 급증과 테마 선택 중 발생하는 오류를 수정했습니다.
- 액자를 그릴 수 없을 때 액자와 작업 영역 예약을 해제해 앱을 계속 사용할 수 있습니다.

## [v0.86](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.86)

- 내장 화면 액자를 SVG로 제작된 **엔틱 골드**, **모던 크롬**, **사이버펑크 네온** 세 가지로 교체했습니다.
- **내 액자 추가**에서 SVG, 투명 PNG, JPEG를 사용할 수 있습니다. SVG는 표시 영역에 맞춰 PNG를 생성하고, JPEG는 가운데 단색 배경을 투명하게 처리합니다.
- Windows 화면 배율이 100%를 넘을 때 테마 메뉴도 대상 화면의 실제 픽셀 해상도를 표시합니다.

## [v0.85](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.85)

- 내장 이미지 액자 100종을 투명 PNG로 바꾸고, 확대·축소할 때 가장자리 번짐과 얇은 장식의 손실을 줄였습니다.
- **내 액자 추가** 버튼 아래에 PNG 전용, 가운데 투명, 대상 화면 권장 해상도 안내를 추가했습니다.

## [v0.84](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.84)

- 가운데가 투명한 PNG를 **화면 액자 → 테마 → 내 액자 추가**에서 가져올 수 있습니다. 가져온 액자는 원본 비율과 투명도를 유지합니다.
- 테마 카드에서 원본 해상도를 확인하고, 대상 화면과 같은 해상도나 FHD·QHD·4K 등으로 거를 수 있습니다.
- 내장 이미지 액자의 어두운 배경 번짐과 가장자리 장식의 늘어짐을 줄였습니다.

## [v0.83](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.83)

- 액자를 켜면 화면 상단 중앙에 실제 종료 단축키가 표시됩니다.
- **화면 액자 설정 → 단축키 로고 보이기**에서 안내를 숨길 수 있습니다. 표시 여부는 자동 저장됩니다.

## [v0.82](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.82)

- **Ctrl+F4**로 툴바가 닫힌 상태에서도 화면 액자를 켜고 끌 수 있습니다.
- **설정 → 단축키 → 화면 액자 켜기/끄기 (전역)**에서 키를 바꿀 수 있습니다.

## [v0.81](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.81)

- **게임 등 전체화면에서도 액자 표시**를 추가했습니다. 테두리 없는 전체화면에서 액자를 표시할지 선택할 수 있습니다.

## [v0.80](https://github.com/gamzagui-collab/ArcDraw/releases/tag/v0.80)

- 브라우저에서 동영상을 전체화면으로 보거나 F11을 눌러도 액자가 화면에 유지됩니다.

이전 버전의 다운로드 및 배포 정보는 [릴리스 목록](https://github.com/gamzagui-collab/ArcDraw/releases)에서 확인할 수 있습니다.
