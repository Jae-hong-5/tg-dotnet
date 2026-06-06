# Architecture Changes Applied

기준 커밋: `60c8e7c Improve realtime pipeline and verification`

이 문서는 아키텍처 리뷰 후 실제 코드에 반영된 변경만 정리한다.

2026-06-06 추가 업데이트:

- 앱 시작 스플래시를 추가했다. MP4 직접 재생 대신 640x360 PNG 122장을 24fps로 재생한다.
- 원본 MP4는 `src/TimeGrapher.App/Assets/Splash/Source/splash5.mp4`에 보관한다.
- 발표/리뷰 문서는 repo 루트에서 `docs/`로 옮겼고, 발표 문서는 청중이 이해하기 쉬운 요약 중심으로 다시 정리했다.

## 실시간 UI 파이프라인

- `MainWindow`에 analysis frame coalescing을 추가했다.
  - 파일: `src/TimeGrapher.App/Views/MainWindow.axaml.cs`
  - analysis thread가 생성한 모든 frame을 UI Dispatcher에 그대로 쌓지 않고, 최신 frame 하나만 pending slot에 유지한다.
  - UI render cadence를 약 30fps(`UI_RENDER_INTERVAL_MS = 33`)로 제한했다.
  - coalesced frame 수는 stderr에 기록한다.

- input overrun 상태를 analysis frame에 포함했다.
  - 파일: `src/TimeGrapher.Core/Shared/AnalysisFrame.cs`
  - 추가 필드: `InputOverrun`, `InputSamplesDropped`
  - UI는 overrun 발생 시 status bar에 drop된 sample 수를 표시한다.

## 분석 워커와 버퍼 처리

- ring buffer overrun 감지를 추가했다.
  - 파일: `src/TimeGrapher.Core/Analysis/AnalysisWorker.cs`
  - pending sample 수가 `MasterAudioBuffer` 용량을 넘으면 가장 최신 window로 reader 위치를 보정하고 drop 수를 frame에 기록한다.

- analysis stop 중 backlog 처리를 중단할 수 있게 했다.
  - 파일: `src/TimeGrapher.Core/Analysis/AnalysisWorker.cs`
  - `HandleInputData()` slice loop 안에서 `_stopRequested`를 확인한다.

- sound image publish cadence를 제한했다.
  - 파일: `src/TimeGrapher.Core/Analysis/AnalysisWorker.cs`
  - 매 analysis frame마다 full `PixelBuffer.Clone()`을 하지 않고, 약 100ms 간격으로 publish한다.

- sync loss/reacquire 시 sound image BPH 상태가 갱신되도록 수정했다.
  - 파일: `src/TimeGrapher.Core/Analysis/AnalysisWorker.cs`
  - 파일: `src/TimeGrapher.Core/Imaging/SoundImageRenderer.cs`
  - `SetBph(0)`이 BPH 무효화와 render state reset을 수행하도록 바꿨다.

## 그래프 렌더링

- scope 표시 데이터에 기본 decimation을 적용했다.
  - 파일: `src/TimeGrapher.App/Rendering/GraphFrameRenderer.cs`
  - sample rate가 48kHz보다 높을 때 display stride를 키워 UI에 전달되는 표시점 수를 줄인다.

- scope history purge 기준을 point count가 아니라 graph tick range 기준으로 보정했다.
  - 파일: `src/TimeGrapher.App/Rendering/GraphFrameRenderer.cs`
  - decimation 이후에도 10초 history 의미가 유지되도록 `maxKey - minKey` 기준으로 정리한다.

- ScottPlot text style obsolete API 사용을 제거했다.
  - 파일: `src/TimeGrapher.App/Rendering/GraphFrameRenderer.cs`
  - `Text.Color`, `FontName`, `FontSize` 대신 `LabelFontColor`, `LabelFontName`, `LabelFontSize`를 사용한다.

## 이미지 렌더링 캐시

- `PixelBufferBitmap`의 static bitmap/scratch cache를 image target별 cache로 변경했다.
  - 파일: `src/TimeGrapher.App/Rendering/PixelBufferBitmap.cs`
  - `ConditionalWeakTable<Image, ImageCache>`를 사용해 여러 window/image control 사이의 mutable bitmap state 공유를 막았다.

## 오디오 입출력

- WAV recording write를 per-sample write에서 span bulk write로 변경했다.
  - 파일: `src/TimeGrapher.Core/AudioIo/WavStreamWriter.cs`
  - little-endian 환경에서는 `MemoryMarshal.AsBytes(samples)`를 이용해 한 번에 쓴다.

- live capture buffer size와 sample-rate 계약을 명확히 했다.
  - 파일: `src/TimeGrapher.Platform.WindowsAudio/AudioCaptureWorker.cs`
  - `WaveInEvent.BufferMilliseconds = 20`을 설정했다.
  - `GetSupportedSampleRates()`를 obsolete 처리하고, 실제 의미에 맞는 `GetCandidateSampleRates()`를 추가했다.
  - live capture의 실제 지원 여부는 `Start()`에서 검증되는 계약으로 정리했다.

- live audio start 실패를 UI에서 명확히 처리하도록 했다.
  - 파일: `src/TimeGrapher.App/Views/MainWindow.axaml.cs`
  - `StartAudioThread()` 실패 시 worker/session을 정리하고 오류 dialog/status를 표시한다.

## Playback WAV 검증

- invalid WAV가 UI device/rate 상태를 먼저 바꾸지 않도록 검증 순서를 수정했다.
  - 파일: `src/TimeGrapher.App/Views/MainWindow.axaml.cs`
  - header 검증 성공 후에만 current directory, playback/sim device, sample rate를 변경한다.

- playback WAV 허용 sample rate 조건을 standard-rate 후보 기준으로 정리했다.
  - 파일: `src/TimeGrapher.App/Views/MainWindow.axaml.cs`
  - 48kHz 고정 문구를 standard-rate 32-bit float mono WAV 기준으로 변경했다.

## Headless Verifier

- verifier 실패 조건을 강화했다.
  - 파일: `src/TimeGrapher.Verify/Program.cs`
  - filename에 expected BPH가 없으면 실패한다.
  - `sync_status != Synced`이면 실패한다.
  - metrics result text가 생성되지 않으면 실패한다.
  - directory 입력의 WAV 처리 순서를 deterministic하게 정렬한다.

## 테스트와 CI

- xUnit 테스트 프로젝트를 추가했다.
  - 파일: `tests/TimeGrapher.Core.Tests/TimeGrapher.Core.Tests.csproj`
  - `SyntheticDetectorTests`: clean synthetic stream의 BPH/sync 검출 회귀 테스트
  - `WavStreamWriterTests`: WAV writer/reader round-trip 테스트

- GitHub Actions CI를 추가했다.
  - 파일: `.github/workflows/ci.yml`
  - locked restore, Release build, test, optional golden WAV verification, Windows publish artifact upload을 수행한다.

- 빌드 재현성을 보강했다.
  - 파일: `global.json`
  - 파일: `Directory.Build.props`
  - 파일: `Directory.Packages.props`
  - 파일: 패키지를 복원하는 App/Platform/Verify/Test 프로젝트의 `packages.lock.json`
  - `TimeGrapher.Core`는 패키지 참조가 없어 lock file 생성을 끈다.
  - .NET SDK version, central package management, lock-file restore를 도입했다.

## 문서

- README에 locked restore, test, test project, central package management, CI 설명을 추가했다.
  - 파일: `README.md`
- 발표/리뷰 문서를 `docs/` 아래로 정리했다.
  - 파일: `docs/README.md`
  - 파일: `docs/ARCHITECTURE_PRESENTATION.md`
  - 파일: `docs/ARCHITECTURE_REVIEW_FIXES.md`
- 발표 문서는 패턴 이름보다 문제, 해결 방향, 검증 결과를 먼저 설명하도록 줄였다.

## 스플래시 시작 화면

- `SplashWindow`를 추가했다.
  - 파일: `src/TimeGrapher.App/Views/SplashWindow.axaml`
  - 파일: `src/TimeGrapher.App/Views/SplashWindow.axaml.cs`
- `splash5.mp4`를 640x360 PNG 122장으로 변환해 앱 리소스로 포함했다.
  - 원본: `src/TimeGrapher.App/Assets/Splash/Source/splash5.mp4`
  - 프레임: `src/TimeGrapher.App/Assets/Splash/splash_0001.png` ... `splash_0122.png`
- Avalonia Pro/MediaPlayer/LibVLC 의존성을 추가하지 않고 Windows와 Raspberry Pi에서 같은 방식으로 보여준다.

## 검증된 명령

아래 명령들이 변경 후 성공했다.

```powershell
dotnet restore TimeGrapherNet.sln --locked-mode
dotnet build TimeGrapherNet.sln -c Release --no-restore --nologo
dotnet test TimeGrapherNet.sln -c Release --no-build --nologo
dotnet run --project src\TimeGrapher.Verify\TimeGrapher.Verify.csproj -c Release --no-build -- D:\TimeGrapher_Refactoring\samples
dotnet publish src\TimeGrapher.App\TimeGrapher.App.csproj -c Release -r win-x64 --self-contained false --no-restore -o artifacts\TimeGrapher.App-win-x64 --nologo
```

2026-06-06 스플래시 추가 후 확인:

```powershell
dotnet build .\TimeGrapherNet.sln -c Debug --no-restore
dotnet test .\TimeGrapherNet.sln -c Debug --no-build
dotnet publish .\src\TimeGrapher.App\TimeGrapher.App.csproj -c Release -r win-x64 --self-contained false --no-restore
dotnet publish .\src\TimeGrapher.App\TimeGrapher.App.csproj -c Release -r linux-arm64 --self-contained true --no-restore
```

추가 결과:

- Windows GUI: 스플래시 후 `TimeGrapher` 메인 창 전환 확인
- Raspberry Pi: `./TimeGrapher.App --smoke` 통과
- Raspberry Pi GUI: `DISPLAY=:0` 실행 12초 유지, stderr 없음
