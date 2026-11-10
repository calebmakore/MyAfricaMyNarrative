# Local setup and demonstration runbook

**Your story. Your voice. Our Africa.**

## Prerequisites

- .NET SDK 10.0.403 or another compatible .NET 10 SDK selected by `global.json`.
- SQL Server Express reachable as `localhost\SQLEXPRESS`.
- FFmpeg and ffprobe. Put their absolute paths in ignored `appsettings.Local.json` files or make both commands available on `PATH`.
- A Chromium-based browser with microphone permission for device recording. Upload remains the fallback.

Never commit API keys, database passwords, media, User Secrets, `appsettings.Local.json`, or `App_Data`.

## Fresh checkout

From the repository root:

```powershell
dotnet restore MyAfricaMyNarrative.slnx
dotnet build MyAfricaMyNarrative.slnx -c Release --no-restore
dotnet tool restore
dotnet ef database update --project src/03.Infrastructure/MyAfricaMyNarrative.Infrastructure/MyAfricaMyNarrative.Infrastructure.csproj --startup-project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj --context ApplicationDbContext
```

The committed connection string targets the development database `MyAfricaMyNarrative` with Windows authentication. Override `ConnectionStrings:DefaultConnection` outside Git when another dedicated database is required. Never point tests at production.

Optional Live OpenAI story assistance belongs in User Secrets for both Web and Worker:

```powershell
dotnet user-secrets set "Ai:Mode" "Live" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Ai:ApiKey" "YOUR_KEY" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Ai:StoryModel" "gpt-6-luna" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Ai:TranscriptionModel" "gpt-transcribe" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
```

Repeat those settings for the Worker project if it will perform AI work. Manual is the safe committed default; missing Live configuration fails startup and never fabricates output.

## Run

Use two terminals from the repository root:

```powershell
dotnet run --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj --urls http://127.0.0.1:5088
dotnet run --project src/03.Infrastructure/MyAfricaMyNarrative.Worker/MyAfricaMyNarrative.Worker.csproj
```

Open `http://127.0.0.1:5088`. Web and Worker must resolve the same database and private-media root.

## Demonstration

1. Register/sign in, create a project, and use Lessons & Help before starting.
2. Record or upload named audio and a named real visual. Confirm permission and playback without downloading.
3. In Shape, save an immutable reviewed transcript/timing range, create source-backed scenes, save creator intention and protected expressions, then compare/reject/apply proposals.
4. Approve the exact story snapshot and record the real fluent reviewer attestation for that revision.
5. In Make, declare only real resources. Generate a plan, make one resource unavailable, and confirm the stale plan is refused and a fresh plan is required.
6. In Export, queue preview/final work with the Worker running. Confirm the final is 1080×1920, H.264/AAC, 60–90 seconds, and that SRT cues follow the approved scene timing.
7. Open authorised artifacts in a new tab. Download the preservation ZIP and verify originals, film, captions, metadata, README and every line in `checksums.sha256`.

## Verification and diagnostics

```powershell
dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj -c Release
dotnet test tests/04.Tests/MyAfricaMyNarrative.IntegrationTests/MyAfricaMyNarrative.IntegrationTests.csproj -c Release
dotnet test tests/04.Tests/MyAfricaMyNarrative.BrowserTests/MyAfricaMyNarrative.BrowserTests.csproj -c Release
```

For the FFmpeg cancellation test, set `MAMN_FFMPEG_PATH` to the installed executable. For public browser smoke checks, set `MAMN_BASE_URL` to the running Web origin. SQL migration checks must use an explicitly named disposable verification database.

Useful diagnostics:

```powershell
dotnet --info
sqlcmd -S "localhost\SQLEXPRESS" -E -C -Q "SELECT @@VERSION"
ffmpeg -version
ffprobe -version
```

If rendering stays queued, start the Worker and verify its database/media configuration matches Web. If a job fails, retain the reviewed story, inspect the bounded failure shown in Export, correct the dependency, and retry the exact snapshot. `progress.md` is the authoritative record of checks actually run.
