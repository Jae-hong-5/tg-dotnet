# TimeGrapherNet Docs

문서는 다음과 같이 나눈다.

## 발표/설명용

- `QT_CPP_TO_AVALONIA_PORTING.md`
  - Qt + C++ 프로젝트를 Avalonia + .NET 8로 전환한 과정을 설명한다
  - Windows와 Raspberry Pi를 함께 지원하기 위해 구조를 어떻게 나눴는지 정리한다
- `ARCHITECTURE_PRESENTATION.md`
  - 듣는 사람이 이해할 수 있게 만든 발표 준비 문서
  - 문제, 해결 방향, 검증 결과를 짧게 설명한다

## 과제 제출용 (Risk / Experiments)

- `RISK_ASSESSMENT.md`
  - 팀 Risk Assessment 문서에 합칠 추가 항목 (R-A2: RPi5 렌더링 성능)
- `PLANNED_EXPERIMENTS.md`
  - technical-experiment-template을 따른 실험 계획
  - 실험 1: RPi5 렌더링 백엔드 성능 A/B 측정 (R-A2 검증용 spike)
- `EXPERIMENT_RESULTS.md`
  - 실험 1 수행 결과 (측정 환경, 백엔드별 결과표, 해석, 권장 사항)

## 작업 기록/근거용

- `ARCHITECTURE_REVIEW_FIXES.md`
  - 아키텍처 리뷰 후 실제 수정한 항목
  - 어떤 문제가 있었고 어떤 코드 방향으로 해결했는지 정리한다

## 원본 작업 기록

초기 포팅과 리뷰 작업의 원문 기록은 `source-notes/`에 번호 순서대로 보관한다.

- `source-notes/00_DotNet_Porting.md`: 초기 포팅 세션 기록과 현재 상태 업데이트
- `source-notes/01_PORTING_PLAN.md`: 초기 구현 계약 기록
- `source-notes/02_ARCHITECTURE_CHANGES.md`: 반영된 구조 변경 기록
- `source-notes/03_ARCHITECTURE_REVIEW_FIXES.md`: 리뷰 반영 기록과 최신 검증 요약

## 빠른 설명

TimeGrapherNet의 핵심 변화는 다음 네 가지다.

1. 분석 로직을 Core로 분리해 UI와 플랫폼에서 떼어냈다.
2. Windows/Pi 입력 구현을 각각 platform 프로젝트로 분리하고 Core의 live-audio 계약 뒤에 둔다.
3. UI는 모든 화면을 무리하게 그리지 않고 최신 결과 중심으로 안정적으로 표시한다.
4. Windows와 Raspberry Pi에서 실행할 수 있도록 RID별 publish 검증을 분리했다.
