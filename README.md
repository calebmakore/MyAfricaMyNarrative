# My Africa, My Narrative

**Your story. Your voice. Our Africa.**

An ASP.NET Core MVC studio for creating a private, source-backed short documentary while preserving original voice, permission evidence, reviewed timing and captions.

## Get the code and run it

These steps take a fresh machine (Windows, PowerShell) from nothing to a running studio. Run every command from the repository root.

### 1. Install the prerequisites

| Need | Version | Notes |
| --- | --- | --- |
| Git | any | To clone the repository. |
| .NET SDK | **exactly 10.0.403** | `global.json` pins this version with roll-forward disabled (see step 3). |
| SQL Server | 2022 Express or later | Expected at `localhost\SQLEXPRESS` with Windows sign-in. |
| FFmpeg + ffprobe | 6.1 or later, with `drawtext` | Needed to render films. `winget install Gyan.FFmpeg` works. |
| EF Core CLI | 10.0.11 | `dotnet tool install --global dotnet-ef --version 10.0.11` |
| Browser | Chromium-based | Microphone permission is needed for in-browser recording; upload always works. |

No AI key is required. The app defaults to **Manual** mode.

### 2. Clone (or pull)

First time:

```powershell
git clone <repository-url> MyAfricaMyNarrative
cd MyAfricaMyNarrative
```

To update an existing checkout:

```powershell
git pull
```

After every pull, re-run the database update in step 5 so new migrations are applied. Then restart the app (and the Worker, if you run one).

### 3. Make sure the pinned .NET SDK is available

```powershell
dotnet --version
```

This must print `10.0.403`. If it doesn't, install that SDK machine-wide or into the git-ignored `.dotnet-sdk` folder, which `global.json` also searches:

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .dotnet-install.ps1
./.dotnet-install.ps1 -Version 10.0.403 -InstallDir .dotnet-sdk -NoPath
```

The `.dotnet-sdk` lookup only works when the `dotnet` on your `PATH` comes from .NET 10 or later.

### 4. Restore and build

```powershell
dotnet restore MyAfricaMyNarrative.slnx
dotnet build MyAfricaMyNarrative.slnx --no-restore
```

If restore fails with HTTP 401 from a private NuGet feed configured on your machine, add `--source https://api.nuget.org/v3/index.json` to the restore command.

### 5. Create or update the database

```powershell
dotnet ef database update --project src/03.Infrastructure/MyAfricaMyNarrative.Infrastructure/MyAfricaMyNarrative.Infrastructure.csproj --startup-project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj --context ApplicationDbContext
```

This creates the development database `MyAfricaMyNarrative` on `localhost\SQLEXPRESS` if it doesn't exist and applies any pending migrations. To use a different server or database, override `ConnectionStrings:DefaultConnection` outside Git, for example with User Secrets or an environment variable. Do this for Web and Worker alike. Never point it at a production database.

### 6. Point the app at FFmpeg (machine-specific, not committed)

If `ffmpeg` and `ffprobe` are already on your `PATH`, skip this step. Otherwise create `appsettings.Local.json` in **both** `src/01.UI/MyAfricaMyNarrative.Web/` and `src/03.Infrastructure/MyAfricaMyNarrative.Worker/`:

```json
{
  "Media": {
    "FfmpegPath": "C:\\path\\to\\ffmpeg.exe",
    "FfprobePath": "C:\\path\\to\\ffprobe.exe"
  }
}
```

These files are git-ignored. Keep keys and passwords out of them and out of every committed file.

### 7. Run

```powershell
dotnet run --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj --urls http://127.0.0.1:5088
```

Open <http://127.0.0.1:5088>, register an account, and create your first story. The web app renders films itself in the background (`Jobs:ProcessInWeb` is `true` by default), so you don't need to start anything else.

The standalone Worker is optional extra rendering capacity. If you run it, it must use the same database and media storage as Web:

```powershell
dotnet run --project src/03.Infrastructure/MyAfricaMyNarrative.Worker/MyAfricaMyNarrative.Worker.csproj
```

Uploaded media and generated films are stored privately under `App_Data/` (git-ignored). With the default `Email:Mode` of `Pickup`, password-reset emails are written as `.eml` files to `App_Data/mail-outbox` instead of being sent.

### 8. Run the tests

```powershell
dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj
dotnet test tests/04.Tests/MyAfricaMyNarrative.IntegrationTests/MyAfricaMyNarrative.IntegrationTests.csproj
```

Integration job-safety tests need a reachable SQL Server (`MAMN_TEST_SQL_SERVER`, default `localhost\SQLEXPRESS`). They create and drop their own throwaway database. Render tests need `MAMN_FFMPEG_PATH` and `MAMN_FFPROBE_PATH`, and browser smoke tests need `MAMN_BASE_URL`. See the runbook for details.

### Optional: Live AI and real email

Manual mode needs no key. To turn on Live OpenAI assistance or SMTP email, set the values with `dotnet user-secrets` as shown in [docs/demo-runbook.md](docs/demo-runbook.md). Do this for Web, and also for the Worker if it renders. If Live mode is selected without a key, the app refuses to start rather than inventing output.

### Troubleshooting

- **`dotnet` reports a different SDK version or "A compatible .NET SDK was not found":** repeat step 3.
- **Cannot connect to SQL Server:** run `sqlcmd -S "localhost\SQLEXPRESS" -E -C -Q "SELECT @@VERSION"`. Then make sure the SQL Server (SQLEXPRESS) service is running.
- **An export stays "Waiting to start" or fails at render:** run `ffmpeg -version`, and check that the paths from step 6 are correct in both projects.

For the full demonstration path and diagnostics, see [docs/demo-runbook.md](docs/demo-runbook.md). Current implementation evidence and any unrun checks are in [progress.md](progress.md).

## Use the pack

1. Extract these files into your application repository root. Keep AGENTS.md capitalised.
2. Open that repository in OpenAI Codex, or run the Codex CLI from its root. Codex reads AGENTS.md as project instructions.
3. Copy **Prompt 1** from [prompts.md](prompts.md) to inspect the repository and start M0/M1.
4. Continue with the numbered prompts, or use the continuous-build prompt for a longer run.
5. Resume from [progress.md](progress.md), preserving actual implementation evidence.

Use Codex against the actual repository. For a hosted environment, make these files and the required dependencies available there.
Start from the repository root so project guidance and relative paths match the intended codebase.
The other Markdown files are supporting specifications: Prompt 1 explicitly directs Codex to read them.

For a configured Codex CLI installation, open a terminal in the repository root and run:

    codex

Then paste Prompt 1 from prompts.md. Keep the normal permission settings for your environment.

## Files

| File | Purpose |
| --- | --- |
| [AGENTS.md](AGENTS.md) | Main Codex project instructions and completion rules. |
| [product.md](product.md) | Creator workflow, cultural preservation and scope. |
| [architecture.md](architecture.md) | Projects, data, AI, private media, jobs and export integrity. |
| [plan.md](plan.md) | Ordered milestones with dependencies and evidence. |
| [design.md](design.md) | Visual system, mobile experience and interactions. |
| [acceptance.md](acceptance.md) | Observable checks for completed behaviour. |
| [prompts.md](prompts.md) | Kickoff, implementation, continuation, review and demo prompts. |
| [progress.md](progress.md) | Honest status, decisions, blockers and handover. |
| [project-description.md](project-description.md) | Submission text with the approved tagline. |
| [sources.md](sources.md) | Research and official technical references. |

## Defaults

- .NET 10 ASP.NET Core MVC, Razor, EF Core and SQL Server.
- CSS tokens and progressive JavaScript, using existing repository conventions.
- GUID identifiers for application-owned records; supported framework Identity schemas.
- Web plus a separate Worker, sharing a durable SQL queue and private storage.
- OpenAI Codex for development. A replaceable OpenAI adapter for runtime AI.
- Real FFmpeg/ffprobe media export, original voice and reviewed captions.
- Manual mode without paid keys; Live for real configured calls; explicitly labelled Fixture mode for tests/demos.

Later explicit user instructions override these assumptions.

## Dependencies and configuration

The full build needs a .NET 10 SDK, SQL Server, FFmpeg/ffprobe and browser-test binaries.
Codex must inspect installed versions, pin compatible dependencies and document actual working commands.
Do not silently downgrade the framework or use in-memory storage as the production database.

Web and Worker need the same database, private storage root and AI settings.
Use a shared development user-secrets ID or explicit equivalent configuration for both hosts.
Keep credentials out of Git; configuration examples contain placeholders only.

## First finished demonstration

A creator records/uploads sources, reviews transcript/timing, protects words, arranges and edits scenes,
adds optional translated captions, plans shots with available resources, approves the cut and credits,
and downloads a real 60–90 second vertical film with burned-in captions and an end credits card.
They can keep originals, reviewed transcripts, context, captions and credits in a portable archive.
Use [acceptance.md](acceptance.md) to assess this behaviour.

Keep long rationale in architecture.md and actual evidence in progress.md.
AGENTS.md stays short enough to remain useful.
