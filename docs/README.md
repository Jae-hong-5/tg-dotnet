# TimeGrapherNet Docs

문서는 두 종류로 나눈다.

## 발표/설명용

- `QT_CPP_TO_AVALONIA_PORTING.md`
  - Qt + C++ 프로젝트를 Avalonia + .NET 8로 전환한 과정을 설명한다
  - Windows와 Raspberry Pi를 함께 지원하기 위해 구조를 어떻게 나눴는지 정리한다
- `ARCHITECTURE_PRESENTATION.md`
  - 듣는 사람이 이해할 수 있게 만든 발표 준비 문서
  - 문제, 해결 방향, 검증 결과를 짧게 설명한다

## 작업 기록/근거용

- `ARCHITECTURE_REVIEW_FIXES.md`
  - 아키텍처 리뷰 후 실제 수정한 항목
  - 어떤 문제가 있었고 어떤 코드 방향으로 해결했는지 정리한다

부모 폴더 `D:\tg_cld\`에도 초기 포팅 기록 문서가 있다.

- `00_DotNet_Porting.md`: 초기 포팅 세션 기록과 현재 상태 업데이트
- `00_PORTING_PLAN.md`: 초기 구현 계약 기록
- `01_ARCHITECTURE_CHANGES.md`: 반영된 구조 변경 기록
- `02_ARCHITECTURE_REVIEW_FIXES.md`: 리뷰 반영 기록과 최신 검증 요약

## 빠른 설명

TimeGrapherNet의 핵심 변화는 다음 세 가지다.

1. 분석 로직을 Core로 분리해 UI와 플랫폼에서 떼어냈다.
2. UI는 모든 화면을 무리하게 그리지 않고 최신 결과 중심으로 안정적으로 표시한다.
3. Windows와 Raspberry Pi에서 실행할 수 있도록 플랫폼 입력과 배포 검증을 분리했다.
