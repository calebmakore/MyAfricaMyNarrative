# Progress and handover

## Current state
- M0 is complete: the documented modular solution exists, targets `net10.0`, restores/builds, pins the locally installed .NET 10.0.403 SDK and EF Core 10.0.11, validates shared configuration, defaults AI to Manual, and records inspected dependencies/capabilities.
- M1 is in progress: GUID Identity, owner-filtered projects, SQL persistence, concurrency tokens, private immutable storage, hashing, signature checks, permission attestation/withdrawal, authorised playback, project lifecycle actions, recording fallback UI and the four-stage shell are implemented.
- The exact product tagline is **Your story. Your voice. Our Africa.**

## Milestones

| ID | State | Evidence |
| --- | --- | --- |
| M0 | Complete | `MyAfricaMyNarrative.slnx` restores and builds with pinned local SDK 10.0.403; EF/Identity packages 10.0.11; SQL Server 2022 Express inspected; configuration and provider capability record below. |
| M1 | In progress | Initial migration applied to `localhost\SQLEXPRESS / MyAfricaMyNarrative`; Web/Domain/Application/Infrastructure slice builds; 6 unit tests pass. Full AC01/AC02/AC03 boundary evidence remains. |
| M2 | Not started | — |
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
- Byte-signature inspection for JPEG, PNG, WebP, MP3, WAV, ISO base-media and WebM; unrecognised input is rejected. Full decode/ffprobe validation is not yet implemented and is not claimed.
- Owner attestation is labelled honestly. Permission withdrawal blocks playback. Authorised media responses support range requests.
- Responsive editorial UI tokens, exact tagline, Record/Shape/Make/Export navigation, real source players and disabled future stages with reasons.
- Modern visual system inspired by the user-provided Thungela Career Pathways reference: self-hosted Jost typography, warm near-black/amber palette, strong editorial hero, rounded layered cards, clearer account screens and responsive navigation. The product retains its own identity and content.
- Progressive microphone capture with refusal/unsupported upload fallback, five-minute cap, preview, unsaved-navigation warning and direct private “Use this recording” persistence. Genuine `.weba`/WebM audio is accepted only after its EBML container signature passes inspection.
- Initial durable `Jobs` SQL table foundation with GUID key, owner/project/target fields, unique deduplication key and `rowversion`; atomic claims/leases are M2 work.

## Dependencies/blockers
- Installed locally in ignored `.dotnet-sdk`: .NET SDK 10.0.403. `global.json` selects it exactly with roll-forward disabled.
- SQL Server 2022 Express 16.0.1210.3 is reachable at `localhost\SQLEXPRESS`; the user-created `MyAfricaMyNarrative` database received only the reviewed initial migration.
- `sqlcmd` 16.0.1000.6 is available.
- FFmpeg/ffprobe are not installed or on PATH. Consequently decoded/probed media metadata and pathological/corrupt-container rejection are incomplete; M1 remains in progress.
- Docker is not installed. Browser executables were not found on PATH and browser automation is unconfigured.
- Loopback Web host started successfully at `http://127.0.0.1:5088`; a separate sandboxed HTTP probe could not access its isolated socket, so no rendered browser claim is made.
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
- Blocked/unrun checks: FFmpeg/ffprobe metadata validation; two-user direct-ID/browser checks; actual upload/playback through a browser; microphone capture persistence; SQL integration suite; browser suite.
- Known defects: ISO base-media signature inspection cannot yet distinguish/fully validate audio M4A from video MP4; deleting database records and local files is not transactional.
- Material user decisions required: none for the next work.
- Next concrete step: install/configure FFmpeg/ffprobe, implement a bounded `ProcessStartInfo.ArgumentList` probe, persist full metadata, add SQL integration tests for two-owner isolation/storage/hash/withdrawal, then run the authenticated browser Record journey.
