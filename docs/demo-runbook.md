# Local setup and demonstration runbook

**Your story. Your voice. Our Africa.**

## Prerequisites

- .NET SDK 10.0.403 or another compatible .NET 10 SDK selected by `global.json`.
- SQL Server Express reachable as `localhost\SQLEXPRESS`.
- FFmpeg 6.1 or later (with `drawtext`, for burned-in captions) and ffprobe. Put their absolute paths in ignored `appsettings.Local.json` files or make both commands available on `PATH`. The caption font (Jost, SIL OFL) is copied beside Web and Worker at build time; set `Media:CaptionFontFile` to use another font, or `Media:BurnInCaptions` to `false` to keep captions only in the SRT.
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

After pulling the 2026-11-10 changes, run the same `dotnet ef database update` command to apply `20261110125834_AddCreditsTranslationsAndShotPlan`, then restart both Web and Worker so they share the new model.

The committed connection string targets the development database `MyAfricaMyNarrative` with Windows authentication. Override `ConnectionStrings:DefaultConnection` outside Git when another dedicated database is required. Never point tests at production.

Optional Live OpenAI story assistance belongs in User Secrets for both Web and Worker:

```powershell
dotnet user-secrets set "Ai:Mode" "Live" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Ai:ApiKey" "YOUR_KEY" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Ai:StoryModel" "gpt-6-luna" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Ai:TranscriptionModel" "gpt-transcribe" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
```

Repeat those settings for the Worker project if it will perform AI work. Manual is the safe committed default; missing Live configuration fails startup and never fabricates output.

## Password reset email

"Forgot your password?" on the sign-in page emails a one-time link. By default `Email:Mode` is `Pickup`: nothing is sent and each message is saved as an `.eml` file in `App_Data/mail-outbox` beside the Web project; open it to follow the link. For real delivery, set these outside Git (User Secrets or environment variables) on the Web project:

```powershell
dotnet user-secrets set "Email:Mode" "Smtp" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Email:SmtpHost" "smtp.example.org" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Email:SmtpUserName" "USER" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Email:SmtpPassword" "PASSWORD" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
dotnet user-secrets set "Email:FromAddress" "no-reply@your-domain" --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj
```

Outside Development also set `Email:PublicBaseUrl` to the site's https address; without it no reset email is sent.

## Run

From the repository root:

```powershell
dotnet run --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj --urls http://127.0.0.1:5088
```

Open `http://127.0.0.1:5088` — signed-out visitors see the landing page; signed-in creators go straight to My stories. The web app renders films itself in the background (`Jobs:ProcessInWeb`, default `true`), so nothing else needs starting; the Export page shows live step-by-step progress and loads the film when it is ready.

The standalone Worker is optional extra capacity (or the only renderer if you set `Jobs:ProcessInWeb` to `false` for Web):

```powershell
dotnet run --project src/03.Infrastructure/MyAfricaMyNarrative.Worker/MyAfricaMyNarrative.Worker.csproj
```

Web and Worker must resolve the same database and private-media root. Both can run at once: jobs are claimed with row locks and the lease is renewed during a render, so each job renders once.

## Demonstration

1. Register/sign in, create a project, and use Lessons & Help before starting.
2. Record or upload named audio and a named real visual. Confirm permission and playback without downloading.
3. In Shape, save an immutable reviewed transcript/timing range, then create scenes. Try saving a “From your recording” scene with altered words to see it refused, then use the exact reviewed words. Save creator intention, protected expressions and an optional second caption language (for example English beside Shona), add a translated caption, and compare/reject/apply proposals. Edit or remove a scene and note that each change is a new revision.
4. Approve the exact story snapshot and record the real fluent reviewer attestation for that revision; translations switch from Draft to Reviewed.
5. Judges' highlight — “Make it with what I have”: in Make, declare one phone, one place, two people and two hours (Time takes hours). Make a plan (AI or practice), approve it, then mark the second person “It fell through” and plan again: both plans appear side by side with changed scenes and “No longer available: …”. Approve the new plan; its filming steps replace the old ones on the shot list.
   In Shape, ask the story coach for structure and gaps, answer its questions in your own words, add an explanation for a local expression and approve it, and drag a scene to reorder. On Record, mark a source as supplied by someone else and record their answer. After exporting, answer “Does it say what you meant?” and open Insights.
5b. In Make, declare only real resources. Suggest shots, make one resource unavailable, and confirm its shots are flagged as blocked. Drop the blocked shot, mark another captured with uploaded footage, and confirm a still-planned shot blocks the final in Export. The resource-plan comparison still refuses stale plans.
6. In Export, add credits (storyteller, contributors, sources) and approve them. Queue a preview or the final film; the Export page shows progress and the film appears when it is ready. Confirm the final is 1080×1920, H.264/AAC, 60–90 seconds including the 5-second credits card, that captions and credits are burned in, and that SRT cues (and the translated SRT) follow the approved scene timing. Change a credit and confirm the earlier output is reported as stale.
7. Open authorised artifacts in a new tab. Download the preservation ZIP and verify originals, film, captions, translated captions, metadata (schema 1.1 with creator direction, answers, credits and production plan), README and every line in `checksums.sha256`.
8. Withdraw permission for a source used in the story and confirm pending exports are cancelled and its outputs can no longer be downloaded or archived.

## Verification and diagnostics

```powershell
dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj -c Release
dotnet test tests/04.Tests/MyAfricaMyNarrative.IntegrationTests/MyAfricaMyNarrative.IntegrationTests.csproj -c Release
dotnet test tests/04.Tests/MyAfricaMyNarrative.BrowserTests/MyAfricaMyNarrative.BrowserTests.csproj -c Release
```

The job-safety tests (`Jobs/JobSafetyTests`) create a throwaway `MAMN_JobTests_<guid>` database from the migrations on `MAMN_TEST_SQL_SERVER` (default `localhost\SQLEXPRESS`, Windows sign-in) and drop it afterwards; they never touch the dev database, and they fail — not skip — if no server is reachable. They prove that with two processors each film renders and saves once, long renders keep their claim, a lost claim stops without saving, and a crashed render is recovered (about 35 s, no FFmpeg needed).

For the FFmpeg cancellation and render-pipeline tests, set `MAMN_FFMPEG_PATH` and `MAMN_FFPROBE_PATH` to the installed executables. For public browser smoke checks, set `MAMN_BASE_URL` to the running Web origin. SQL migration checks must use an explicitly named disposable verification database.

Useful diagnostics:

```powershell
dotnet --info
sqlcmd -S "localhost\SQLEXPRESS" -E -C -Q "SELECT @@VERSION"
ffmpeg -version
ffprobe -version
```

If rendering stays “Waiting to start”, check that the web app is running with `Jobs:ProcessInWeb` true (or that a Worker is running) and that its database/media configuration matches Web. If a job fails, retain the reviewed story, inspect the bounded failure shown in Export, correct the dependency, and retry the exact snapshot. `progress.md` is the authoritative record of checks actually run.
