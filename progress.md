# Progress and handover

## Current state
- M0 is complete: the documented modular solution exists, targets `net10.0`, restores/builds, pins the locally installed .NET 10.0.403 SDK and EF Core 10.0.11, validates shared configuration, defaults AI to Manual, and records inspected dependencies/capabilities.
- M1 is complete: GUID Identity, owner-filtered projects, SQL persistence, concurrency tokens, private immutable storage, hashing, signature plus ffprobe decode checks, permission attestation/withdrawal, authorised playback, recording fallback and direct-ID isolation have actual evidence.
- M2 is complete: manual reviewed transcripts/timing, immutable scenes/revisions, exact snapshot approval, export readiness, SQL claims/leases/retries, FFmpeg rendering, SRT timeline mapping and authorised artifact downloads produced an approved source-backed preview with real audio and visuals.
- The exact product tagline is **Your story. Your voice. Our Africa.**

## Milestones

| ID | State | Evidence |
| --- | --- | --- |
| M0 | Complete | `MyAfricaMyNarrative.slnx` restores and builds with pinned local SDK 10.0.403; EF/Identity packages 10.0.11; SQL Server 2022 Express inspected; configuration and provider capability record below. |
| M1 | Complete | SQL persistence and private media are live; authenticated synthetic user B received 404 for user A's project, Shape, Export/jobs, source playback, artifact download, rename and permission withdrawal direct IDs; recording/upload persistence has observed evidence. |
| M2 | Complete | Approved revision 3 produced real PreviewVideo/SRT artifacts. Independent ffprobe confirms H.264/AAC streams, 540×960 preview dimensions and 37.021 s duration; durable job succeeded in one repaired attempt. |
| M3 | Not started | — |
| M4 | Not started | — |
| M5 | Not started | — |
| M6 | Not started | — |
| M7 | Not started | — |

Allowed states: Not started, In progress, Blocked, Complete. Complete requires evidence from plan.md and relevant acceptance scenarios.

## Implemented behaviour
- ASP.NET Core MVC Web and Worker hosts with Domain, Application and Infrastructure boundaries.
- ASP.NET Core Identity with GUID users/roles, local registration/login/logout and secure framework cookies.
- Owner-filtered create/list/open/rename/archive/restore/delete project actions; SQL `rowversion` concurrency token and a creator-facing conflict outcome.
- Private originals outside `wwwroot`, generated opaque keys, canonical traversal checks, streamed SHA-256 hashing, bounded upload/project counts and sizes, and cleanup on failed persistence/project deletion.
- Byte-signature inspection plus bounded ffprobe decode/stream/metadata validation for supported uploads; unrecognised, corrupt and streamless input is rejected.
- Owner attestation is labelled honestly. Permission withdrawal blocks playback. Authorised media responses support range requests.
- Responsive editorial UI tokens, exact tagline, Record/Shape/Make/Export navigation, real source players and disabled future stages with reasons.
- Modern visual system inspired by the user-provided Thungela Career Pathways reference: self-hosted Jost typography, warm near-black/amber palette, strong editorial hero, rounded layered cards, clearer account screens and responsive navigation. The product retains its own identity and content.
- Progressive microphone capture with refusal/unsupported upload fallback, five-minute cap, preview, unsaved-navigation warning and direct private “Use this recording” persistence. Genuine `.weba`/WebM audio is accepted only after its EBML container signature passes inspection.
- Recording UX now has a pulsing microphone indicator, elapsed timer, explicit disabled/grey recording button, review state, adjacent recording-specific permission confirmation and visible save failure text. Successful saves are confirmed in Sources and expose a direct “Continue to Shape” action.
- Manual Shape workflow: immutable reviewed transcript revisions with source timing/playback, source-backed scenes, reorder by new revision and exact snapshot approval.
- Make workflow: declared equipment, people, locations, time and budget resources with availability state and visible scene/coverage status.
- Export workflow: readiness reasons, deduplicated durable queue, SQL Server `UPDLOCK`/`READPAST` atomic claims, expiring attempt leases, bounded retries, stale-attempt rejection and worker recovery.
- Real FFmpeg pipeline using `ProcessStartInfo.ArgumentList` with shell execution disabled: trims original audio, combines real visuals, emits portrait H.264/AAC MP4 and remapped SRT, then ffprobes streams/dimensions/duration before publishing immutable artifacts.

## Dependencies/blockers
- Installed locally in ignored `.dotnet-sdk`: .NET SDK 10.0.403. `global.json` selects it exactly with roll-forward disabled.
- SQL Server 2022 Express 16.0.1210.3 is reachable at `localhost\SQLEXPRESS`; the user-created `MyAfricaMyNarrative` database received only the reviewed initial migration.
- `sqlcmd` 16.0.1000.6 is available.
- FFmpeg/ffprobe 9.0.2 (Gyan full build) are installed through WinGet and configured only in ignored `appsettings.Local.json` files. No credentials were added.
- Docker is not installed. Browser executables were not found on PATH and browser automation is unconfigured.
- Loopback Web host and Worker start successfully outside the restricted process sandbox; the login route returned HTTP 200 with the exact tagline. Both were stopped for the final build and should be restarted after it.
- Testhost execution hangs inside the restricted process sandbox but passed outside it; this is recorded in the validation log.
- No provider key is configured or required. AI mode remains Manual; no Live call was made.
- Jost Regular/Medium/SemiBold/Bold is self-hosted under `wwwroot/fonts/jost`; the included `OFL.txt` records the SIL Open Font License. No runtime Google Fonts request is required.

## Provider capability record

| Provider/model/endpoint | Actual supported timing/response/schema | Source/date | Verification |
| --- | --- | --- | --- |
| OpenAI `/v1/audio/transcriptions`; model not selected | Accepted response formats and timing are model-dependent. `timestamp_granularities` requires `verbose_json`; segment/word support must be confirmed for the selected model. Current documentation notes the existing timestamp workflow and a 25 MB transcription input limit. | Official OpenAI API transcription reference and speech-to-text guide, inspected 2026-11-08. | Documentation inspection only; no key/call. |
| OpenAI Responses API; story model not selected | Structured Outputs uses `text.format` with `type: json_schema`; `strict: true` supports a documented JSON Schema subset. Schema adherence does not prove source fidelity. | Official OpenAI Structured Outputs guide and Responses API reference, inspected 2026-11-08. | Documentation inspection only; no key/call. |

## Validation log

| Date | Milestone/scenario | Exact command/observed action | Actual outcome | Evidence |
| --- | --- | --- | --- | --- |
| 2026-11-08 | M0 | `dotnet --info` before local SDK install | Only SDK 11.0.100-rc.3 was initially present; .NET 10.0.14 runtime/reference packs existed. | Tool output; prompted local SDK installation. |
| 2026-11-08 | M0 | Official `dotnet-install.ps1 -Channel 10.0 -Quality GA -InstallDir .dotnet-sdk -NoPath` | Installed SDK 10.0.403 locally; `dotnet --version` resolves 10.0.403 through `global.json`. | Successful installer and version output. |
| 2026-11-08 | M0 | `dotnet restore MyAfricaMyNarrative.slnx --disable-build-servers -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json -v:minimal` | Succeeded for all 8 projects. The explicit official source bypassed an unrelated configured private feed returning HTTP 401. | Restore output. |
| 2026-11-08 | M0 | `dotnet build MyAfricaMyNarrative.slnx --no-restore --disable-build-servers -m:1 -v:minimal` | Succeeded with 0 warnings and 0 errors using SDK 10.0.403. | All 8 project outputs built. |
| 2026-11-08 | M1 / AC18 partial | `sqlcmd -S 'localhost\SQLEXPRESS' -d MyAfricaMyNarrative -E -C -Q 'SELECT DB_NAME(), @@VERSION' -l 10 -b` | Succeeded; dedicated database and SQL Server 2022 Express verified. | Returned `MyAfricaMyNarrative`, SQL 16.0.1210.3. |
| 2026-11-08 | M1 | `dotnet-ef migrations add InitialCreate ...` then `dotnet-ef database update ...` | Initial migration generated, reviewed for Identity/application tables, GUID keys, indexes and rowversion, then applied successfully. | `20261108211408_InitialCreate`; history row product version 10.0.11. |
| 2026-11-08 | M1 / AC02 partial | Unit tests for project invariants, explicit permission withdrawal, PNG byte detection and unknown-content rejection. | Passed. Boundary corrupt-container/hash/persistence checks remain unrun. | Included in 6-test result. |
| 2026-11-08 | M0/M1 | `dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj --no-build --no-restore --disable-build-servers -m:1 -v:minimal` | Outside restricted process sandbox: 6 passed, 0 failed, 0 skipped, 406 ms. Inside sandbox the testhost hung before execution. | Exact test runner summary. |
| 2026-11-08 | M1 startup partial | `dotnet run --project src/01.UI/MyAfricaMyNarrative.Web/MyAfricaMyNarrative.Web.csproj --no-build --no-launch-profile --urls http://127.0.0.1:5088` | Host started in Development and listened on 127.0.0.1:5088. Data-protection keys persist under ignored `App_Data`; key-at-rest encryption is not configured for local development. | Hosting lifetime output; rendered HTTP/browser check unrun. |
| 2026-11-08 | M1 UI | Inspected `https://thungela-test.puresurvey.co.za/` in a browser and read rendered design properties. | Reference uses Jost, `#2A2320`, `#FAF7F2`, `#FBB72E`, 12–16 px radii and layered low shadows; these cues informed an original My Africa, My Narrative treatment. | Browser screenshot and computed styles. |
| 2026-11-08 | M1 UI | `dotnet build MyAfricaMyNarrative.slnx --no-restore --disable-build-servers -m:1 -v:minimal` | Succeeded with 0 warnings and 0 errors after stopping the running Web host that initially locked its output DLLs. | All 8 projects built. |
| 2026-11-08 | M1 UI | `dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj --no-build --no-restore --disable-build-servers -m:1 -v:minimal` | 6 passed, 0 failed, 0 skipped, 287 ms. | Exact test runner summary. |
| 2026-11-08 | M1 UI / AC16 partial | Reloaded authenticated Projects and Details screens in the in-app browser; checked computed layout. | Jost loaded locally; exact tagline present; `scrollWidth` equalled `clientWidth` (1086 px), proving no horizontal overflow at the observed desktop viewport. Mobile/browser matrix remains unrun. | Rendered screenshots and DOM-computed values. |
| 2026-11-08 | M1 / AC03 partial | Built the direct MediaRecorder-to-private-upload flow and inspected the authenticated Record UI without granting microphone permission. | “Start microphone” and “Stop and review” have a measured 16 px gap; review exposes “Use this recording”; `.weba` is accepted; solution build succeeded and 6 unit tests passed. Actual microphone permission/capture remains a user-device check. | Browser DOM/screenshot, build output and test result. |
| 2026-11-09 | M1 / WebA upload repair | Inspected `0432a4a9-3037-43ee-9741-f1d11cf1fd27.weba` (338,396 bytes; EBML/WebM signature), then exercised its authenticated upload with permission checked and attribution blank. Repaired implicit-required validation/null trimming, disposed the temporary destination before its Windows rename, and configured domain-generated `MediaAsset.Id` values with `ValueGeneratedNever()` so EF inserts rather than updates. Ran `dotnet build MyAfricaMyNarrative.slnx --no-restore --disable-build-servers -m:1 -v:minimal` and the focused unit-test command. | Exact WebA upload succeeded, generic/error pages were absent, the filename appeared under Sources and an audio player was rendered. Build: 0 warnings/errors. Tests: 7 passed, 0 failed/skipped, 755 ms. The supplied file was persisted once in the user's project as part of the authorised verification. | Browser DOM result, diagnostic exception traces, build output and test runner summary. |
| 2026-11-09 | M1 UI / static assets | Added `UseStaticFiles()` before routing after the no-launch-profile local host returned `/css/site.css` as HTTP 200 with zero bytes even though the static-asset manifest was present. Rebuilt the solution and reopened the authenticated project page. | Build succeeded with 0 warnings/errors. Browser loaded 102 CSS rules; computed body font is Jost and background is `rgb(250, 247, 242)`. Styled project/source cards were visually confirmed. | HTTP response inspection, browser computed styles and screenshot. |
| 2026-11-09 | M1/M2 schema | Generated, reviewed and applied `20261109174109_StudioWorkflow` with media metadata, transcript/story/approval/resource/artifact tables and durable job fields. | `dotnet-ef database update` succeeded against the dedicated `MyAfricaMyNarrative` SQL Express database. | EF command output and migration history. |
| 2026-11-09 | M1 media | Installed FFmpeg/ffprobe 9.0.2 and configured their inspected paths in ignored local settings. Added bounded ffprobe validation before private persistence. | Build succeeded; actual upload probing is implemented. Existing pre-migration WebA retains duration 0 and is not falsely backfilled. | Installed package/path inspection and build output. |
| 2026-11-09 | M2 implementation | `dotnet build MyAfricaMyNarrative.slnx --no-restore --disable-build-servers -m:1 -v:minimal` | Succeeded: 0 warnings, 0 errors across all 8 projects. | Exact build summary, 1m15s. |
| 2026-11-09 | M2 invariants / AC11/AC12 partial | `dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj --no-build --no-restore --disable-build-servers -m:1 -v:minimal` outside restricted process sandbox. | 10 passed, 0 failed, 0 skipped, 560 ms. Includes snapshot hash change, private setters, active lease exclusion, expired reclaim and stale-token rejection. | Exact test runner summary. |
| 2026-11-09 | M2 host/worker smoke | Started Web and Worker with local configuration; queried `http://127.0.0.1:5088/Account/Login`. | Web HTTP 200, title `Sign in - My Africa, My Narrative`, exact tagline present; Worker connected to SQL and repeatedly performed idle atomic claim queries without error. | Host logs and loopback response. |
| 2026-11-09 | M1 / recording save repair | Added a packet-timestamp duration fallback for valid MediaRecorder WebM files that omit container duration; made fetch inspect the server-rendered error after redirects instead of treating it as success; added recording indicator/timer, saved/failed states and Continue to Shape handoff. Ran `dotnet build MyAfricaMyNarrative.slnx --no-restore --disable-build-servers -m:1 -v:minimal` and `node --check src/01.UI/MyAfricaMyNarrative.Web/wwwroot/js/site.js`. | Build succeeded with 0 warnings/errors; JavaScript syntax check succeeded. Reloaded the authenticated Record page: existing audio remained in Sources and the new Continue to Shape link was present. Actual microphone permission/capture remains a user-device check. | Build output, Node syntax check and rendered accessibility tree. |
| 2026-11-09 | M2 / AC12–AC14 preview render | Diagnosed failed job `a16684d4-c9cd-4d09-aca4-67b8b0e3226a`: Web and Worker resolved `App_Data/private-media` under different content roots. Changed both hosts to the same repository-local storage root, copied four existing originals without deleting their prior copies, added failed/cancelled logical-job retry, rebuilt, and retried the exact job. | Worker claimed and completed the job in one attempt with two verified artifacts: PreviewVideo 893,955 bytes, SHA-256 `8745c59973c6…`; SRT 94 bytes, SHA-256 `05f97de729a6…`. Job state is Succeeded with no failure. Renderer performed its configured ffprobe stream/dimension/duration check before publication. | Worker logs and read-only SQL query of Jobs/ExportArtifacts. |
| 2026-11-09 | M2 export usability | Changed current artifact actions to open authorised MP4/SRT responses in a new isolated tab (`target="_blank"`, `rel="noopener"`). | Export workspace remains open while the creator reviews an artifact. | Razor source inspection and Web build. |
| 2026-11-09 | M6 help and onboarding | Added keyboard-accessible contextual `?` buttons and a native modal help dialog across Record, Shape, Make and Export. Added a public Lessons & Help page covering purpose, requirements, privacy, timing, stages, job states, artifacts and common questions; linked it from primary navigation for signed-in and signed-out users. Ran Web build, JS syntax check and loopback page probe. | Web build: 0 warnings/errors. `node --check` passed. `/Home/Help` returned HTTP 200 with all four stages and the shared dialog present. | Build output, Node check and HTTP response inspection. |
| 2026-11-09 | M1 recording state | Corrected author CSS that overrode the native `hidden` attribute. After a successful recording save, the review blob/player/permission state is cleared and hidden before navigation to Sources. | A saved recording is represented under Sources only; the unsaved review is not duplicated under Record. | Web build and JavaScript syntax check. |
| 2026-11-09 | M1 / AC01 ownership boundary | Registered a random, visibly synthetic local test user through the real account endpoint and exercised user A's known direct IDs as authenticated user B. Checked project Details, Shape, Export/job listing, source playback, artifact download, project rename and permission withdrawal. | Every foreign read and mutation returned HTTP 404. User B could not list/read/change user A's project, media, jobs or exports. | Authenticated HTTP sessions with anti-forgery validation; exact status summary retained in session output. |
| 2026-11-09 | M2 / AC13–AC14 independent boundary | Queried artifact `393a554c-38ce-4b37-bfc1-c6d2717171d6`, confirming it binds to approved immutable revision 3/hash `fdaba477…`; ran installed ffprobe directly on stored artifact bytes. | Video and audio streams present; 540×960 preview; duration 37.021354 seconds. Paired SRT artifact is 94 bytes. This completes M2 preview evidence; M7 still requires the 60–90 second 1080×1920 final. | SQL joins across artifact/job/revision/approval plus independent ffprobe JSON. |
| 2026-11-09 | M1 source naming | Added creator-facing `DisplayName` separate from immutable `OriginalName`, recording/upload name entry, existing-source rename and friendly Shape selectors. Applied `20261109194718_AddMediaDisplayName` with existing rows backfilled from original filenames. | Migration succeeded; full build 0 warnings/errors; 11 unit tests passed including rename preserving the original filename. | EF migration output, build and test summaries. |

## Decisions

| Decision | Reason |
| --- | --- |
| OpenAI Codex is the development assistant; AGENTS.md is the entry point. | Latest explicit user instruction. |
| MVC, Razor, EF Core/SQL Server and modular Domain/Application/Infrastructure boundaries. | Architecture specification. |
| Pin .NET SDK 10.0.403 and EF Core/Identity/EF CLI 10.0.11. | Versions were actually inspected, installed/restored and built together. |
| Keep AI Manual by default and leave model IDs empty. | No key/provider call is required for M0/M1, and unverified capabilities must not appear configured. |
| Store Identity framework join/claim tables with their supported key shapes; application tables use GUID primary keys. | Security-compatible exception documented by architecture.md. |

## Session handover
- Files changed: solution/projects under `src/` and `tests/`; `global.json`, `.gitignore`, migration, configuration, UI/assets and this progress record.
- Blocked/unrun checks: 60–90 second final source-backed documentary; full automated SQL integration suite; browser viewport suite; Live AI (no provider key/model selected); fluent language review.
- Known defects: cancellation does not yet interrupt an in-flight FFmpeg child; preservation ZIP and AI proposal/replanning workflows are not implemented; deleting database records and local files is not transactional.
- Material user decisions required: none for the next work.
- Next concrete step: upload/select a permitted visual, create and approve source-backed scenes, run Worker to produce and ffprobe a real MP4/SRT, then add cancellation/cleanup and SQL competing-worker/ownership tests before M3.
