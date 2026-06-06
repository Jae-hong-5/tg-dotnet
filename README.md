# TimeGrapherNet

Qt/C++ TimeGrapher(`D:\TimeGrapher_Refactoring`)를 **Avalonia + C# (.NET 8)** 로 포팅한
cross-platform 데스크톱 앱. 시계 틱 오디오에서 비트레이트(BPH), 레이트 오차(s/d), 비트 에러(ms),
진폭(°)을 실시간 추정하고 스코프/레이트 플롯과 폴디드 사운드 이미지로 시각화한다.

## 빌드 / 실행

```powershell
dotnet restore TimeGrapherNet.sln --locked-mode
dotnet build TimeGrapherNet.sln -c Release      # 첫 복원은 사내망에서 ~3분
dotnet test TimeGrapherNet.sln -c Release
dotnet run --project src/TimeGrapher.App        # GUI
dotnet run --project src/TimeGrapher.Verify -c Release -- D:\TimeGrapher_Refactoring\samples
```

Raspberry Pi 5 / ARM64 self-contained publish:

```powershell
dotnet publish .\src\TimeGrapher.App\TimeGrapher.App.csproj -c Release -r linux-arm64 --self-contained true
```

Pi GUI 실행에는 XWayland/Avalonia 의존성(`libx11-6`, `libice6`, `libsm6`, `libfontconfig1`,
`xwayland`)이 필요하다. Pi live audio는 먼저 PipeWire source를 `wpctl status`로 열거하고
`pw-record` raw float mono stream을 분석 pipeline에 공급한다. PipeWire source가 없으면
ALSA capture hardware를 `arecord -l`로 열거하고 `arecord` raw S16 mono stream으로 fallback한다.
capture source가 없으면 UI는 `Playback/Sim`만 표시한다.

Pi에서 화면 없이 audio 상태를 확인할 때:

```bash
./TimeGrapher.App --audio-smoke
./TimeGrapher.App --capture-smoke --duration-ms=1500
```

`--audio-smoke`는 PipeWire/ALSA capture source 목록을 출력한다. `--capture-smoke`는 첫 source를
짧게 열고 `samples_written`을 출력하며, source가 없으면 exit code 2를 반환한다.

## 프로젝트 구성

| 프로젝트 | 내용 |
|---|---|
| `TimeGrapher.Core` | UI/플랫폼 무관 로직 — 검출 코어(tg_* 포트), 메트릭, 사운드 이미지 렌더러, WAV reader/writer, 시뮬레이터, 분석 워커 |
| `TimeGrapher.App` | Avalonia 11.3 UI — MainWindow, 정보 탭 목록/전달자, ScottPlot 렌더러, 플랫폼별 live audio 선택 |
| `TimeGrapher.Platform.WindowsAudio` | Windows live audio backend — NAudio WaveInEvent capture, Windows endpoint volume helpers |
| `TimeGrapher.Platform.LinuxAudio` | Linux/Pi live audio backend — PipeWire `pw-record` capture, ALSA `arecord` fallback, source probing |
| `TimeGrapher.Verify` | 헤드리스 검증 콘솔 — 샘플 WAV의 파일명 BPH와 검출 BPH 비교, 전부 일치 시 exit 0 |
| `TimeGrapher.Core.Tests` | xUnit 회귀 테스트 — 합성 시계 신호 검출, WAV writer/reader round-trip |
| `TimeGrapher.App.Tests` | 정보 탭 규칙, 렌더링 데이터 규칙, UI 전달 데이터 축소 회귀 테스트 |
| `TimeGrapher.Platform.LinuxAudio.Tests` | Linux/Pi audio source parser와 process timeout 계약 테스트 |

문서:

- `docs/README.md`: 문서 읽는 순서
- `docs/QT_CPP_TO_AVALONIA_PORTING.md`: Qt/C++에서 Avalonia/.NET으로 전환한 과정
- `docs/ARCHITECTURE_PRESENTATION.md`: 발표용 아키텍처 개선 정리
- `docs/ARCHITECTURE_REVIEW_FIXES.md`: 아키텍처 리뷰 반영 내역
- `docs/source-notes/`: 초기 포팅/계약/리뷰 원문 기록

기술 매핑: Qt Widgets→Avalonia, QCustomPlot→ScottPlot.Avalonia, Qt Multimedia→플랫폼별
audio backend(Windows는 NAudio WaveInEvent, Linux/Pi는 PipeWire `pw-record` + ALSA `arecord` fallback), QImage→PixelBuffer(ARGB32)→WriteableBitmap,
QThread/signal→전용 Thread + AutoResetEvent + `Dispatcher.UIThread.Post`. WPF 미사용.

Core는 WindowsAudio/NAudio/PipeWire를 참조하지 않는다. live audio backend는 Core의 작은
`ILiveAudioWorker` 계약 뒤에서 선택되고, 실행 중인 Live/Playback/Sim 입력 worker는
공통 `IAudioInputWorker` lifecycle로 pause/stop/data-ready를 처리한다.
`win-x64` publish에는 WindowsAudio만, `linux-arm64` publish에는 LinuxAudio만 들어가도록
프로젝트 참조를 RID 기준으로 분리한다.

패키지 버전은 `Directory.Packages.props`에서 중앙 관리하고 `packages.lock.json`을 커밋한다.
CI는 `dotnet restore --locked-mode`, Release build, test, generated/edge WAV verifier,
Windows publish와 publish smoke를 수행한다.

Windows publish artifact는 현재 framework-dependent(`--self-contained false`)이다. 실행 환경에는
.NET 8 Runtime이 필요하다. 외부 배포용으로 런타임 설치 전제를 없애려면 self-contained
publish artifact를 별도로 만든다.

## 스플래시 리소스

앱 시작 시 640x360 borderless `SplashWindow`를 중앙에 띄우고, 24fps PNG 시퀀스를 재생한 뒤
`MainWindow`로 전환한다.

- 원본 영상: `src/TimeGrapher.App/Assets/Splash/Source/splash5.mp4`
- 앱 리소스: `src/TimeGrapher.App/Assets/Splash/splash_0001.png` ... `splash_0122.png`
- 프레임 생성 예:

```powershell
ffmpeg -i .\src\TimeGrapher.App\Assets\Splash\Source\splash5.mp4 -vf scale=640:360 -vsync 0 .\src\TimeGrapher.App\Assets\Splash\splash_%04d.png
```

## 검증 상태 (2026-06-06)

- `dotnet build` 오류 0
- `dotnet test TimeGrapherNet.sln -c Release` 통과
- `linux-arm64 --self-contained true` publish 통과
- Raspberry Pi 5: `./TimeGrapher.App --smoke` 통과, `DISPLAY=:0` GUI 실행 유지,
  스플래시 길이보다 긴 12초 동안 stderr 없이 실행 유지
- 현재 테스트 Pi에는 PipeWire/ALSA capture source가 없어 live microphone 입력은 물리 검증 대기
- 헤드리스 검증: 샘플 9/9 BPH 정확 검출 (18000/21600/28800), sync 전부 Synced,
  레이트 ±20 s/d 이내, 진폭 206–320°, 비트에러 0.0–1.3 ms
- GUI 간단 실행 확인: 실행 후 장치 열거/기본값 로드 정상, 크래시 없음
- 스플래시 간단 실행 확인: Windows Debug/Release publish에서 스플래시 후 `TimeGrapher` 메인 창 전환 확인,
  Raspberry Pi `DISPLAY=:0` GUI 실행 12초 유지 및 stderr 없음

## 원본과의 의도적 차이 (요약)

- `setRenderSource`의 QObject 동적 프로퍼티 기록(런타임 진단용)은 Avalonia 대응 개념이 없어 생략
- QCP 마커 화살촉 글리프는 단순 선분으로 근사 (위치/길이는 동일)
- Qt 동영상 스플래시 대신 MP4에서 생성한 640x360 PNG 시퀀스를 Avalonia 리소스로 재생
- AGC 비활성화(WindowsAudio.cpp의 device-topology 순회)는 NAudio 미노출로 엔드포인트 볼륨 설정만 수행

세부 편차는 각 소스 파일 주석 참조.
