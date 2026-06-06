# 실험 1: RPi5 렌더링 백엔드 성능 A/B 측정 (GPU 가속 vs 소프트웨어)

[technical-experiment-template](https://github.com/pmerson/technical-experiment-template/blob/master/technical-experiment-template_ko-KR.md) 기반.
리스크 R-A2(RPi5에서 GPU 가속 렌더링이 오히려 느리다는 보고)를 검증하기 위한 실험이다.

## 결과 및 권장 사항

TO-DO: 실험 완료 후 기록. (수행 결과는 EXPERIMENT_RESULTS.md에 정리)

## 목적

답해야 하는 기술적 질문:

> **Q1.** RPi5에서 GPU 가속 렌더링(GLX/EGL)이 소프트웨어 렌더링보다 느린가?
> (커뮤니티 보고: 가속 약 80ms vs 소프트웨어 6–12ms — 사실이면 실시간 그래프가 끊긴다)
>
> **Q2.** 우리 앱의 실시간 그래프(Rate/Scope)가 RPi5에서 요구 갱신율(10Hz 이상)을 만족하는가?

이 답으로 **"RPi5 배포 시 Avalonia 렌더링 백엔드를 무엇으로 고정할 것인가"** 라는 설계 결정이 내려진다.
영향 범위: 앱 시작 설정(`Program.cs`), RPi 배포 가이드.

배경: 보고의 출처를 추적하면 단일 커뮤니티 사례(Avalonia Discussion #18807, 앱 측 버그로 종결)지만,
일반화할 수 없으므로 우리 실제 워크로드로 실기기에서 측정해야 확신할 수 있다.

## 상태

계획됨

## 예상 산출물

- 앱에 내장된 재사용 가능한 벤치마크 하니스 (CLI 실행, 결과 JSON 출력)
- 백엔드별(GLX / EGL / Software) 프레임타임 비교표 (FPS, 평균, p95, p99)
- 렌더링 백엔드 선택 권장안 (기본값 유지 또는 Software 강제)

## 필요한 자원

- Raspberry Pi 5 (모니터 연결, SSH 접근) — 팀 공용 장비
- Windows 개발 PC (.NET 8 SDK, linux-arm64 크로스 publish)
- 작업 공수: 약 1인일

## 실험 설명

1. **벤치 하니스 구현**: 앱에 진단용 CLI 모드를 추가한다.
   - `--render-mode=glx|egl|software`: 렌더링 백엔드를 폴백 없이 고정 (실패하면 명확히 실패하도록)
   - `--render-bench`: Sim 모드 런을 자동 시작 → 매 컴포지션 프레임마다 실제 그래프 파이프라인을
     강제 리드로우(평상시보다 가혹한 부하) → 프레임 간격 30초 수집 → 통계를 JSON으로 출력 후 자동 종료
   - GL 프로브: 실제 생성된 GL 컨텍스트의 렌더러 문자열 기록
     (진짜 하드웨어 가속(V3D)인지, 소프트웨어 폴백(llvmpipe)인지 판별 — RPi에는 glxinfo가 없음)
2. **Windows에서 하니스 동작 검증** (짧은 측정으로 종단 확인)
3. **RPi5 배포**: linux-arm64 self-contained publish → SSH로 전송, `--smoke`로 기동 확인
4. **3개 백엔드 측정**: 각 워밍업 5초 + 측정 30초

   ```bash
   DISPLAY=:0 ./TimeGrapher.App --render-bench --render-mode=glx --bench-label=pi5-glx
   ```

5. 결과 비교 → 백엔드 권장안 도출 → 본 문서와 Risk Assessment(R-A2)에 기록

**완료 기준**: ① 3개 백엔드 모두 30초 측정 완료, ② GL 렌더러 문자열로 하드웨어 가속 여부 확인,
③ 백엔드 선택 권장안 도출 — 세 가지가 모두 충족되면 실험 종료.

## 기간

1주차 내 완료 목표 (약 1인일).

## 링크 및 참고 자료

- 원 보고: [Avalonia Discussion #18807 — Poor Linux performance when using hardware acceleration](https://github.com/AvaloniaUI/Avalonia/discussions/18807)
- 관련 사례: [Discussion #18942 — RPi 고해상도 전체 리페인트 저하](https://github.com/AvaloniaUI/Avalonia/discussions/18942)
- [Avalonia 공식 — Raspberry Pi에서 DRM으로 실행](https://docs.avaloniaui.net/docs/guides/platforms/rpi/running-on-raspbian-lite-via-drm)
