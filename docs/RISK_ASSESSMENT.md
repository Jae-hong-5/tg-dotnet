# Risk Assessment 추가 항목 (렌더링 성능)

> 팀 Risk Assessment 문서에 합칠 항목. ID(R-A2)는 기존 문서 번호에 맞게 조정한다.

---

R-A2 — RPi5에서 GPU 가속 렌더링이 소프트웨어 렌더링보다 느려(커뮤니티 보고: 가속 ~80ms vs 소프트웨어 6~12ms) 실시간 그래프(Rate/Scope) 갱신이 끊긴다

품질요소: Performance (UI Frame Rate / Latency)

근거: Avalonia GitHub Discussion #18807 (RPi5 사용자 보고), pdf (p.25 Real Time Performance)

발생 확률 / 영향: Medium / High

완화 방향: 1주차 spike로 RPi5 실기기에서 렌더링 백엔드(GLX/EGL/Software) A/B 측정 후 백엔드 확정. 가속 경로가 느리면 Software 렌더링으로 전환(설정 1줄, 기능 손실 없음)

Tradeoff point: 렌더링 백엔드는 UI 프레임 안정성↔CPU 점유(Software 렌더링은 오디오 분석 스레드와 CPU 경쟁)의 tradeoff point

코멘트: 보고의 출처를 추적한 결과 단일 사례(앱 측 버그로 종결)였으나 우리 워크로드에서의 실측 확인 필요. 1주차 spike(Planned Experiments 실험 1) 결과로 렌더링 백엔드 기본값 유지/변경 결정
