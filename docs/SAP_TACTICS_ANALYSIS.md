# SAP 기준 Architecture Tactics & Design Patterns 분석

> CMU-LG Software Architecture Training Course 과제 문서.
> 기준 교재: Bass·Clements·Kazman, *Software Architecture in Practice* (이하 **SAP**).
>
> 이 문서는 TimeGrapherNet에 실제로 적용된 tactic·pattern을 **코드 근거로 검증**해 정리한다.
> 표의 마지막 열은 교과서 정의에 대한 적용도다 — **✓ 완전 적용**, **△ 유사하나 부분 적용**, **✗ 기각**.

## 개요 — 아키텍처를 지배하는 한 가지 문제

TimeGrapher는 시계 소리를 받아 실시간으로 분석·표시하는 앱이다(입력 → 검출 → 측정 → 화면).
실시간 앱이므로 설계는 세 가지 압력에서 출발한다.

1. **성능** — UI 주 스레드가 막히면 화면이 멈춘다.
2. **변경용이성** — 분석 로직이 UI·OS와 섞이면 바꾸기 어렵다.
3. **이식성** — Windows와 라즈베리파이 5를 한 코드로 돌려야 한다.

아래의 거의 모든 tactic은 이 세 압력에서 파생된다.

---

## 1. Architecture Tactics (품질속성별)

### 변경용이성 (Modifiability)

| Tactic | 적용 방식 | 근거 | |
|---|---|---|---|
| **restrict dependencies** | Core는 외부 참조 0개. App→Platform→Core 단방향 비순환. **CI가 grep으로 Core 안의 NAudio/Platform 참조를 차단**하고, OS별 publish에 잘못된 DLL이 섞이면 빌드 실패 | `Core.csproj`, `ci.yml:44` | ✓ |
| **encapsulate** | OS 오디오 스택(NAudio / pw-record)을 Core 소유 인터페이스 `ILiveAudioWorker : IAudioInputWorker` 뒤에 은닉 | `IAudioInputWorker.cs` | ✓ |
| **use an intermediary** | `LiveAudioBackend` 한 파일만 구체 OS 타입을 알고 분기. 나머지 App은 인터페이스만 사용 | `LiveAudioBackend.cs:65` | ✓ |
| **increase semantic coherence** | Core = 분석(Detection/Metrics/Imaging/Sim)만 담당. UI·OS 책임 없음 | `Core.csproj` | ✓ |
| **split module** | 비대해지던 `MainWindow`를 partial 5개 + 추출 서비스(`RunCommandService`, `RunSessionController`, `SelectionCoordinator`)로 분해 | `MainWindow.*.cs` | ✓ |

### 성능 (Performance) — 실시간 UI의 핵심

| Tactic | 적용 방식 | 근거 | |
|---|---|---|---|
| **introduce concurrency** | 입력·분석·녹음이 각자 전용 스레드(분석은 `Priority.Highest`), UI 스레드는 렌더링만. 생산자는 소비자를 기다리지 않음 | `AnalysisWorker.cs:92` | ✓ |
| **limit event response** | 렌더 스케줄러가 **"최신 프레임 1개"만 유지** — 렌더 진행 중 들어온 프레임은 병합/폐기(`_droppedFrames`)하고, 일회성 신호(오버런 등)는 병합으로 보존 | `AnalysisFrameRenderScheduler.cs:34` | ✓ |
| **schedule resources** | 모든 탭은 가벼운 `ObserveFrame`만 받고, **활성 탭만** 무거운 `RenderFrame` 수행 | `AnalysisFrameRouter.cs:19` | ✓ |
| **bound queue sizes** | 녹음 큐 = `BlockingCollection(128)`. 초과 시 블록을 **드롭**(분석 스레드를 막지 않음) | `QueuedWavStreamWriter.cs` | ✓ |
| **reduce overhead** | 롤링 집계 O(1)(`RollingAverage/LeastSquares`), 그래프 점 수를 예산(8000/250)으로 다운샘플(`SeriesDataReducer`), `ArrayPool`·비트맵 재사용 | 다수 | ✓ |
| **manage sampling rate** | 입력 워커를 Stopwatch 기준 10ms 주기로 페이싱; 노이즈 플로어를 매 샘플이 아닌 ~1ms마다 데시메이션 | `SimWorker.cs:190`, `Detector.cs:643` | ✓ |
| **maintain multiple copies of data** | 30초 링버퍼로 읽기/쓰기 속도 분리 + 프레임마다 PixelBuffer를 **불변 복제**해 UI가 안전하게 읽는 동안 분석 스레드는 계속 갱신 | `MasterAudioBuffer`, `SoundPrintFrameProjector.cs:87` | ✓ |

### 가용성 (Availability) — 시작/중지 안정화

| Tactic | 적용 방식 | 근거 | |
|---|---|---|---|
| **timestamp (논리 시퀀스)** | **핵심.** 실행마다 단조증가 `_runSessionToken`을 발급, 모든 비동기 콜백이 토큰을 들고 옴 → 이전 실행의 늦은 응답을 토큰 불일치로 폐기(`AnalysisSessionId`, 렌더 `_generation`까지 3중) | `RunSessionController.cs:165` | ✓ |
| **exception handling / detection** | 워커 스레드는 예외를 try/catch로 가둬 프로세스를 죽이지 않고 `Failed`로 보고; `_stopRequested`로 "정상 중지"와 "장치 사망"을 구분해 `CaptureEnded` 발생 | `PlaybackWorker.cs`, `AudioCaptureWorker.cs:162` | ✓ |
| **degradation** | Linux에서 PipeWire 미가용 시 ALSA(`arecord`, S16_LE)로 폴백해 저하된 형태로라도 캡처 지속 | `LinuxLiveAudioWorker.cs` | ✓ |

### 시험용이성 (Testability)

| Tactic | 적용 방식 | 근거 | |
|---|---|---|---|
| **sandbox + limit nondeterminism** | `WatchSynthStream`이 SplitMix64 PRNG를 **시드 고정**해 결정론적 시계 신호 생성. `Clean()`은 모든 확률 요소를 끔 | `WatchSynthStream.cs:315` | ✓ |
| **abstract data sources** | mic·WAV·합성이 모두 `IAudioInputWorker`/`engine.Process(span)` 뒤에서 동일하게 소비되어, 파일로 결정론적 검증 가능 | `DetectorMetricsEngine.cs` | ✓ |
| **specialized interfaces** | GUI 없는 `Verify` 콘솔, `--smoke/--audio-smoke` 진입점(종료코드 0/2/3), `InternalsVisibleTo` 테스트 훅 | `Verify/Program.cs`, `Program.cs:12` | ✓ |
| **executable assertions** | Verify가 파일명의 기대 BPH와 검출 BPH를 대조해 exit code 반환 → **CI가 매 푸시 실행** | `Verify/Program.cs:119` | ✓ |
| **limit structural complexity** | 파서·리듀서·라우터·서비스를 작은 단일책임 단위로 분리, 22개 테스트 파일이 개별 타깃 | tests/ | ✓ |

### 사용성·이식성 (Usability / Portability)

| Tactic | 적용 방식 | 근거 | |
|---|---|---|---|
| **pause/resume** (Usability) | `WorkerPauseGate`(ManualResetEventSlim + Volatile)가 워커 루프를 50ms 슬라이스로 멈추되 정지 요청에는 즉시 반응 | `WorkerPauseGate.cs` | ✓ |
| **defer binding** (Portability) | RID(`win-x64`/`linux-arm64`)에 따라 Platform 참조·`DefineConstants`를 조건부로 바인딩 → 같은 소스로 OS별 앱 생성 | `App.csproj:47` | △ |

---

## 2. Design Patterns

| Pattern | 적용 방식 | 근거 | |
|---|---|---|---|
| **Layers** | App / Platform.* / Core 3계층, 하향 의존만 + CI 강제 | `App.csproj` | ✓ |
| **Adapter** | `AudioCaptureWorker`가 NAudio를, `LinuxLiveAudioWorker`가 pw-record/arecord를 `ILiveAudioWorker`로 변환 | Platform.* | ✓(Win) / △(Linux: 프로세스 오케스트레이션 성격) |
| **Factory** | `LiveAudioBackend.CreateWorker`, `IRecordingWriterFactory`, `InfoTabRegistry`(kind→factory 딕셔너리) | 다수 | ✓ |
| **Strategy** | 탭별 렌더러 `IAnalysisFrameConsumer`(TabId로 선택), 입력 모드 `IAudioInputWorker`를 동일하게 구동 | `IAnalysisFrameConsumer.cs` | ✓ |
| **Command** | `RelayCommand`/`AsyncRelayCommand`(ICommand) — 재진입 차단 + CanExecute 재질의 | `AsyncRelayCommand.cs` | ✓ |
| **Observer** | 워커 이벤트(`DataReady`, `AnalysisFrameReady`, `CaptureEnded`) 구독·정지 시 해제 | `AnalysisWorker.cs:57` | ✓ (브로커형 Pub-Sub은 아님) |
| **Producer-Consumer (bounded)** | 분석→`WavWriter` 스레드를 `BlockingCollection`으로 분리 | `QueuedWavStreamWriter.cs` | ✓ |
| **Shared-Data** | `MasterAudioBuffer`(단일 writer/reader 동기화 링버퍼) | `MasterAudioBuffer.cs` | ✓ (Blackboard 아님) |
| **MVVM** | VM이 바인딩 상태·ICommand 보유, XAML은 로직 없음 | `MainWindowViewModel.cs` | △ |
| **Pipe-and-Filter** | HPF→Envelope→Delay→Detector 단계형 데이터플로 | `TgDetector.cs:237` | △ |
| **Map-Reduce** | — | — | ✗ 기각 |

---

## 3. 적용도에 대한 정직한 평가 (채점 포인트)

검증 단계에서 **단어만 비슷한 과잉 주장**을 다음과 같이 교정했다. 이 구분 자체가 SAP 학습의 핵심이다.

- **MVVM (△):** 바인딩·커맨드는 진짜지만, **시작/중지 생명주기가 아직 code-behind**(`MainWindow.RunLifecycle.cs`)에 있고 서비스가 VM 상태를 직접 변경한다 → "실용적 부분 MVVM". 발표 자료도 이를 인정.
- **Pipe-and-Filter (△):** 단계 구조는 맞지만 **단일 스레드 동기 호출 체인**이다. 진짜 동시 파이프 경계는 두 곳뿐 — `입력→링버퍼→분석`, `분석→녹음 큐`.
- **defer binding (△):** RID **빌드/배포 시점** 바인딩이라, 교과서가 강조하는 런타임 플러그인/지연 로딩(가장 늦은 바인딩)은 아니다. 카탈로그에서 가장 약한 바인딩 시점.
- **Map-Reduce (✗ 기각):** 분할·병렬·셔플이 전혀 없는 **증분 슬라이딩-윈도우 집계**일 뿐 → `reduce overhead` tactic으로 봐야 한다.
- **기타 교정:**
  - "bound execution times"는 사실 정지 join의 **대기 상한(2초)**이다(워커 작업 자체엔 시간 상한 없음).
  - "retry"는 자동이 아니라 **사용자가 다시 누르면 멱등 재시도**다.
  - PipeWire→ALSA는 fault-recovery `reconfiguration`이 아니라 시작 시 **`degradation` 폴백**이다.
  - stale 콜백 폐기는 `ignore faulty behavior`가 아니라 `timestamp`의 stale 탐지 절반이다.

---

## 4. 가장 인상적인 설계 3가지 (발표 권장)

1. **CI로 강제되는 의존성 경계** — 아키텍처 규칙을 문서가 아닌 *실패하는 테스트*로 못박았다(architecture fitness function). `ci.yml`이 Core의 OS 의존을 grep으로 차단하고, OS별 산출물에 잘못된 DLL이 섞이면 실패시킨다.
2. **"최신 프레임만" 렌더 + 활성 탭만 렌더** — 실시간 UI 멈춤을 막는 성능 tactic 집합(`limit event response` + `schedule resources` + `reduce overhead`).
3. **단조 run-session token** — 실시간 시작/중지의 stale-response 버그를 구조적으로 차단한다(`timestamp` tactic).
