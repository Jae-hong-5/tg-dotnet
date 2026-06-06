# Architecture Review Fixes

이 문서는 기존 아키텍처 리뷰 항목을 재확인한 뒤, "한 화면에 12개 그래프"가 아니라 "정보 화면 탭이 최대 12개로 늘어나는 구조"라는 기준으로 적용한 수정 내용을 정리한다.

2026-06-06 추가 업데이트:

- 스플래시 화면을 추가했다. MP4 직접 재생 대신 640x360 PNG 122장을 24fps로 재생한다.
- 발표 문서는 전문 용어보다 문제, 해결 방향, 검증 결과가 먼저 보이도록 `TimeGrapherNet\docs\ARCHITECTURE_PRESENTATION.md`에 다시 정리했다.
- 최신 검증 기준은 Core tests 31개, App tests 49개 통과와 Windows/Pi smoke다.

## 적용 기준

- 오른쪽 정보 영역은 여러 개의 정보 탭으로 확장된다.
- 한 번에 렌더링해야 하는 것은 모든 탭이 아니라 현재 활성 탭이다.
- Core는 분석 데이터와 series ID만 생산하고, 탭 배치, 스타일, refresh 정책은 App이 소유한다.
- UI frame coalescing은 유지하되, 오래된 frame drop이 데이터 손실이 되지 않아야 한다.

## 주요 수정

### 1. 화면 갱신 밀림 안전화

문제:

- `MainWindow`는 최신 분석 결과 하나만 유지하며 오래된 결과를 덮어쓴다.
- 기존 파형 그래프 데이터는 조금씩 이어 붙이는 방식이었기 때문에 UI 렌더가 밀리면 sample/marker가 누락될 수 있었다.

수정:

- `AnalysisWorker`가 파형 데이터를 2초 범위의 최신 묶음으로 유지한다.
- 파형 그래프 series는 누적 추가가 아니라 교체 가능한 최신 묶음으로 발행한다.
- 파형 marker/text도 단일 분석 결과 기준이 아니라 최신 표시 구간 기준으로 발행한다.
- rate graph와 results text도 최신 값을 저장해 이후 분석 결과에 계속 실어 보낸다.

주요 파일:

- `src/TimeGrapher.Core/Analysis/AnalysisWorker.cs`
- `src/TimeGrapher.Core/Shared/AnalysisFrame.cs`
- `tests/TimeGrapher.Core.Tests/AnalysisFrameContractTests.cs`

### 2. Active tab 중심 frame routing

문제:

- 탭이 늘어날수록 `MainWindow`가 모든 탭 렌더링 결정을 직접 알게 될 위험이 있었다.
- inactive tab까지 매 frame 갱신하면 탭 수 증가에 따라 UI 비용이 커질 수 있다.

수정:

- `InfoTabCatalog`를 추가해 탭 ID, 제목, refresh interval, graph snapshot 계약을 정의했다.
- `AnalysisFrameRouter`를 추가해 현재 활성 탭 consumer에만 frame을 전달한다.
- `RateScopeFrameConsumer`, `SoundPrintFrameConsumer`를 통해 탭별 frame 처리를 분리했다.

주요 파일:

- `src/TimeGrapher.App/Tabs/InfoTabCatalog.cs`
- `src/TimeGrapher.App/Tabs/AnalysisFrameRouter.cs`
- `src/TimeGrapher.App/Tabs/IAnalysisFrameConsumer.cs`
- `src/TimeGrapher.App/Rendering/RateScopeFrameConsumer.cs`
- `src/TimeGrapher.App/Rendering/SoundPrintFrameConsumer.cs`
- `tests/TimeGrapher.App.Tests/InfoTabCatalogTests.cs`
- `tests/TimeGrapher.App.Tests/AnalysisFrameRouterTests.cs`

### 3. Plot rebuild 제거

문제:

- 기존 `GraphFrameRenderer`는 매 frame Scatter plottable을 제거하고 다시 만들었다.
- active Rate/Scope 탭 하나만으로도 UI thread budget을 크게 소비할 수 있었다.

수정:

- ScottPlot plottable은 `CreateGraphs` / `Reset` 시점에만 생성한다.
- frame render 시에는 backing list만 교체한다.
- series별 target point budget을 적용해 과도한 표시점을 decimation한다.
- 현재 활성 탭의 renderer만 refresh한다.

주요 파일:

- `src/TimeGrapher.App/Rendering/GraphFrameRenderer.cs`
- `src/TimeGrapher.App/Tabs/InfoTabCatalog.cs`

### 4. 오른쪽 정보 영역 layout 개선

문제:

- 기존 window와 graph tab 영역은 고정 크기 absolute `Canvas`에 직접 배치되어 있었다.
- 정보 탭이 늘어날 때 오른쪽 영역 확장과 탭별 화면 구성이 어려웠다.

수정:

- 왼쪽 control panel은 기존 geometry를 유지했다.
- 오른쪽 results/tab 영역은 resize 가능한 `Grid` 기반으로 변경했다.
- 기존 `Rate/Scope`, `Sound Print` 탭에는 tab ID를 `Tag`로 부여해 router와 연결했다.

주요 파일:

- `src/TimeGrapher.App/Views/MainWindow.axaml`
- `src/TimeGrapher.App/Views/MainWindow.axaml.cs`

### 5. MainWindow 책임 최소 분리

문제:

- `MainWindow`가 session 설정 조립, UI enable/disable, WAV 검증, renderer 호출을 모두 직접 처리했다.

수정:

- `AnalysisRunSettings`로 analysis worker config 조립을 분리했다.
- run/stop UI enable 상태 적용은 `MainWindow`와 ViewModel 쪽으로 정리했다.
- `WavProbe`로 WAV header 검증과 playback 파서를 통합했다.
- `MainWindow`는 현재 tab ID와 render context를 만들어 router에 전달하는 역할로 축소했다.

주요 파일:

- `src/TimeGrapher.App/AnalysisRunSettings.cs`
- `src/TimeGrapher.App/ViewModels/MainWindowViewModel.cs`
- `src/TimeGrapher.Core/AudioIo/WavProbe.cs`
- `src/TimeGrapher.Core/AudioIo/PlaybackWorker.cs`
- `src/TimeGrapher.App/Views/MainWindow.axaml.cs`

### 6. 테스트와 CI 보강

수정:

- `TimeGrapher.App.Tests` 프로젝트를 추가했다.
- 탭 catalog 계약, graph snapshot 계약, inactive tab routing을 테스트한다.
- synthetic detector test를 대표 BPH 여러 개로 확장했다.
- CI에서 warning-as-error build를 사용하도록 변경했다.
- CI test 결과 TRX artifact 업로드를 추가했다.

주요 파일:

- `tests/TimeGrapher.App.Tests/TimeGrapher.App.Tests.csproj`
- `tests/TimeGrapher.App.Tests/InfoTabCatalogTests.cs`
- `tests/TimeGrapher.App.Tests/AnalysisFrameRouterTests.cs`
- `tests/TimeGrapher.Core.Tests/AnalysisFrameContractTests.cs`
- `tests/TimeGrapher.Core.Tests/SyntheticDetectorTests.cs`
- `.github/workflows/ci.yml`
- `TimeGrapherNet.sln`

### 7. 스플래시와 문서 정리

수정:

- `SplashWindow`를 추가해 앱 시작 시 640x360 PNG 시퀀스를 보여준다.
- 원본 `splash5.mp4`는 `Assets/Splash/Source/`에 보관하고, 앱 리소스에는 PNG frame만 포함한다.
- repo 루트에는 `README.md`만 남기고 발표/리뷰 문서는 `docs/`로 옮겼다.
- 발표 문서는 듣는 사람이 이해하기 쉽도록 "문제 → 해결 → 결과" 순서로 줄였다.

주요 파일:

- `src/TimeGrapher.App/Views/SplashWindow.axaml`
- `src/TimeGrapher.App/Views/SplashWindow.axaml.cs`
- `src/TimeGrapher.App/Assets/Splash/`
- `docs/README.md`
- `docs/ARCHITECTURE_PRESENTATION.md`
- `docs/ARCHITECTURE_REVIEW_FIXES.md`

## 검증 결과

다음 명령을 실행해 통과를 확인했다.

```powershell
dotnet restore TimeGrapherNet.sln --locked-mode
dotnet build TimeGrapherNet.sln -c Release --no-restore /p:TreatWarningsAsErrors=true
dotnet test TimeGrapherNet.sln -c Release --no-build
dotnet publish src\TimeGrapher.App\TimeGrapher.App.csproj -c Release -r win-x64 --self-contained false -o artifacts\TimeGrapher.App-win-x64
```

2026-06-06 최신 추가 확인:

- `TimeGrapher.Core.Tests`: 31 passed
- `TimeGrapher.App.Tests`: 49 passed
- Windows Debug build: 경고 0, 오류 0
- Windows Release publish: `--smoke` exit code 0, 스플래시 후 `TimeGrapher` 창 전환 확인
- Raspberry Pi linux-arm64 publish: `./TimeGrapher.App --smoke` 통과
- Raspberry Pi GUI: `DISPLAY=:0` 실행 12초 유지, stderr 없음

## 남은 주의사항

- 새 정보 탭을 추가할 때는 `InfoTabCatalog`에 tab definition을 추가하고, 해당 tab의 `IAnalysisFrameConsumer` 구현을 등록해야 한다.
- graph를 포함하는 탭은 화면 갱신 누락에 안전하도록 최신 묶음 교체 방식을 유지해야 한다.
- external WAV fixture가 확보되면 CI에서 optional verifier를 mandatory gate로 승격할 수 있다.
