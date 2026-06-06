# 실험 1 결과: RPi5 렌더링 백엔드 성능 A/B 측정

- 실험 계획: [PLANNED_EXPERIMENTS.md](PLANNED_EXPERIMENTS.md)
- 대상 리스크: R-A2 (Avalonia 사용 시 RPi5에서 GPU 가속 렌더링이 SW 렌더링보다 느릴 수 있다)
- 수행일: 2026-06-06 (1주차 spike, 당일 완료)

## 한 줄 결론

**보고는 우리 앱에서 재현되지 않았다. GPU 가속이 SW 렌더링보다 오히려 빨랐고, 렌더링 설정은 기본값(GPU 우선)을 유지한다.**

## 실험 환경

| 항목 | 값 |
|------|----|
| 기기 | Raspberry Pi 5 (4코어 Cortex-A76, 16GB) |
| OS / 세션 | Debian 13 (trixie), labwc(Wayland) + XWayland 경유 |
| 그래픽 드라이버 | Mesa 25.0.7 (V3D) |
| 화면 / 창 크기 | 1280×800 모니터, 앱 창 1280×722 (사실상 전체 화면) |
| 워크로드 | Sim 모드(28800bph)로 실제 분석→렌더 파이프라인 가동, 매 컴포지션 프레임마다 Rate/Scope 그래프 강제 리드로우 |
| 측정 | 백엔드별 워밍업 5초 + 측정 30초, 프레임 간격 통계 수집 |

워크로드 참고: 평상시 앱은 약 10–12Hz로만 갱신하면 충분하다.
이 실험은 매 프레임 리드로우를 강제했으므로 **실사용보다 5배 이상 가혹한 조건**이다.

## 측정 결과

| 렌더링 백엔드 | GL 렌더러 | FPS | 평균 | p50 | p95 | p99 | 최대 |
|--------------|-----------|:---:|:---:|:---:|:---:|:---:|:---:|
| GLX (GPU 가속) | Broadcom V3D 7.1.10.2 | 59.2 | 16.9ms | 16.4ms | 18.6ms | 21.0ms | 476.7ms* |
| EGL (GPU 가속) | Broadcom V3D 7.1.10.2 | 60.0 | 16.7ms | 16.3ms | 19.2ms | 21.7ms | 31.0ms |
| Software (CPU) | — | 43.6 | 22.9ms | 22.2ms | 30.0ms | 33.7ms | 38.2ms |

\* 측정 30초 중 1회성 스파이크 (p99가 21.0ms이므로 시작 직후 과도기로 판단)

## 해석

1. **GPU 가속 두 백엔드 모두 화면 주사율(약 60Hz) 한계까지 도달했다.**
   평균 16.7–16.9ms는 vsync 간격(16.7ms)과 일치한다. 즉 GPU는 더 빠를 수 있는데 화면이 60Hz라 묶인 상태다.
   보고된 "가속 약 80ms"는 어디서도 재현되지 않았다.
2. **진짜 하드웨어 가속이 동작했음을 확인했다.**
   GL 렌더러 문자열이 `V3D 7.1.10.2`(RPi5의 VideoCore GPU)로 기록됐다.
   소프트웨어 폴백(llvmpipe)이었다면 측정 자체가 무효였을 것이다.
3. **SW 렌더링이 오히려 더 느렸다** (43.6fps, 평균 22.9ms). 보고와 방향이 반대다.
   다만 43.6fps도 앱 요구치(10Hz 이상)의 4배가 넘으므로, 어느 백엔드든 이 앱에는 충분하다.
4. 원 보고(#18807)는 출처 추적 결과 해당 사용자 앱의 거대 배경 이미지(ImageBrush)가
   GPU 캐시를 넘쳐 생긴 문제로 종결됐고, 우리 앱에는 그런 요소가 없다. 실측 결과와 일치한다.

## 완료 기준 충족 여부

| 완료 기준 | 충족 |
|-----------|:---:|
| ① 3개 백엔드(GLX/EGL/Software) 모두 30초 측정 완료 | O |
| ② GL 렌더러 문자열로 하드웨어 가속 여부 확인 | O |
| ③ 백엔드 선택 권장안 도출 | O |

## 권장 사항

1. **렌더링 설정을 바꾸지 않는다.** Avalonia 기본값(GPU 우선, Software 폴백)을 유지한다.
2. 벤치 하니스는 코드에 유지한다. 배포 환경이 바뀌면(고해상도 모니터 등) 30초 만에 재측정할 수 있다.
3. 대형 이미지/ImageBrush는 추가하지 않는다 (원 보고의 실제 원인).
4. **리스크 R-A2는 발생 확률을 Low로 하향하고 종결한다.**

## 재현 방법

```bash
# RPi5에서 (linux-arm64 self-contained publish 배포 후)
DISPLAY=:0 ./TimeGrapher.App --render-bench --render-mode=glx      --bench-label=pi5-glx
DISPLAY=:0 ./TimeGrapher.App --render-bench --render-mode=egl      --bench-label=pi5-egl
DISPLAY=:0 ./TimeGrapher.App --render-bench --render-mode=software --bench-label=pi5-sw
# 결과는 stdout에 "RENDER_BENCH_RESULT {json}" 한 줄로 출력됨
```

하니스 코드: `src/TimeGrapher.App/Diagnostics/`, `src/TimeGrapher.App/Views/MainWindow.RenderBench.cs`
