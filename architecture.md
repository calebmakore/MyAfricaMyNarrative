# Architecture

## 1. Application shape

Use a .NET 10 ASP.NET Core MVC modular application with two executable hosts:
Web for creator interactions, Worker for durable AI/media jobs. Share SQL Server and private storage.
Keep deployment simple; this is not a collection of microservices.

| Project path | Responsibility | References |
| --- | --- | --- |
| src/01.UI/MyAfricaMyNarrative.Web | MVC, Razor, Identity UI, authorised media endpoints. | Application; Infrastructure for composition. |
| src/02.Logic/MyAfricaMyNarrative.Domain | Entities, revisions, readiness and source/permission invariants. | No application project. |
| src/02.Logic/MyAfricaMyNarrative.Application | Use cases, DTOs, validation, authorisation and contracts. | Domain. |
| src/03.Infrastructure/MyAfricaMyNarrative.Infrastructure | EF Core, SQL jobs, private storage, AI and FFmpeg adapters. | Application, Domain. |
| src/03.Infrastructure/MyAfricaMyNarrative.Worker | Atomic job claims, execution, leases and recovery. | Application, Infrastructure. |
| tests/04.Tests/MyAfricaMyNarrative.UnitTests | Fast domain/use-case checks. | Relevant production projects. |
| tests/04.Tests/MyAfricaMyNarrative.IntegrationTests | Actual SQL Server, storage and media boundaries. | Relevant production projects. |
| tests/04.Tests/MyAfricaMyNarrative.BrowserTests | Browser creator journeys. | Running/test-host Web. |

Use MyAfricaMyNarrative.slnx at the root.
Pin the actual inspected .NET 10 SDK in global.json and compatible EF Core package versions.
Use built-in dependency injection and purposeful application services, without generic repositories that simply repeat EF APIs.

## 2. Persistence and identity

Use SQL Server in development, test and deployment. Review EF migrations before applying them to the intended local/test target.
Verify migrations against a fresh dedicated test database.

All application-owned tables, including explicit join entities, use GUID primary keys and appropriate unique constraints.
Use ASP.NET Core Identity with GUID user/role IDs. Identity's internal claims/join tables retain supported framework
key shapes; document this exception rather than claiming every framework table has a GUID primary key.
Do not customise security-critical framework internals for cosmetic consistency.
Ownership and policies, not GUID unpredictability, provide access control.

Use UTC dates and SQL Server rowversion for mutable aggregates.
Concurrent edits return a reviewable conflict instead of silently overwriting another tab.

| Record | Important data/invariant |
| --- | --- |
| Project | OwnerUserId, title, private status, languages, current revision pointers, rowversion. |
| IntentRevision | Immutable audience, meaning, tone, protected expressions and creator context. |
| MediaAsset | ProjectId, private opaque key, original name, SHA-256, actual format/size/duration/dimensions and status. |
| PermissionRecord | Asset, actor, scope, attribution, declaration method, grant/withdrawal dates. Label owner attestation honestly. |
| TranscriptRevision | Asset, text/language, origin, reviewer/time; immutable reviewed versions. |
| TranscriptSegment | TranscriptRevisionId, known speaker, StartMs/EndMs, reviewed exact text and timing provenance. |
| CulturalTerm | Original expression, creator explanation, language and approved handling; included in snapshots. |
| StoryRevision | Immutable ordered story/intent/term snapshot. Saving changed work creates a new revision. |
| Scene | Revision, explicit order, purpose, text origin, protected state, timing and visual readiness. |
| SceneSourceLink | Scene, asset and reviewed transcript revision/segment with actual source range. |
| ProductionPlanRevision | Story revision, immutable constraints, proposed/approved production plan. |
| Shot | Plan, scene, declared resources, duration and missing-media state. |
| ReviewApproval | Actor, exact revision/snapshot hash, approval kind/scope/time. |
| Job | Kind, project/owner, versioned typed payload, target hash, state, lease, attempts and cancellation. |
| ExportArtifact | Job, snapshot hash, private keys, file hashes, verified metadata and current permission eligibility. |

Make order unique within a revision and reject cross-project references server-side.
Caption cues, credits and export settings may be validated structured payloads inside a versioned story snapshot.
Introduce additional tables only when useful querying/editing requires them.

## 3. Private media

Store originals and outputs outside wwwroot and serve through authorised endpoints.
LocalPrivateStorage behind IPrivateMediaStorage is sufficient initially.
Use server-generated GUID keys, canonical paths and traversal checks. Original filenames are metadata.

Initial configurable limits: 20 MiB per upload, 10 source assets and 200 MiB per project.
Limit input recordings/clips to five minutes for the pilot. Target film duration is 60–90 seconds.
Align app, host and reverse-proxy upload limits. Upload one asset per multipart request; avoid base64 JSON.

Accept recognised JPEG/PNG/WebP, MP3/WAV/M4A/WebM audio and MP4/WebM video.
Inspect actual signatures and decoded/probed metadata. Do not trust MIME headers/extensions alone.
Reject unsupported, oversized, corrupt and pathological inputs before processing.
Normalised provider inputs must also satisfy current provider limits.

Originals are immutable. Derivatives use separate keys.
Withdrawal/deletion immediately blocks new use and current downloads, cancels affected jobs,
and schedules safe cleanup of original/derived files according to the recorded action.
The app cannot retrieve offline copies already downloaded.

## 4. Contracts

| Interface | Responsibility |
| --- | --- |
| ICurrentUser / IProjectAccessService | Identity and project ownership. |
| IPrivateMediaStorage | Bounded streaming write/read/delete and safe key resolution. |
| IMediaProbe | Real ffprobe metadata. |
| ITranscriptionService | Actual text/timing and capability/origin metadata. |
| IStoryCoach | Questions and source-backed story proposals from creator-approved inputs. |
| IProductionPlanner | Shots using the exact declared resource set. |
| IExportReadinessService | Source, timing, duration, review, missing-media and permission checks. |
| IJobStore | Durable enqueue, atomic claim, leases, cancellation and idempotent completion. |
| IFilmRenderer | Render a server-validated immutable manifest into actual files. |
| IArchiveExporter | Authorised portable ZIP with media, context and versioned metadata. |

AI adapters never grant access or mark creator approvals. Keep provider DTOs separate from persisted domain models.

## 5. AI

OpenAI Codex is the development assistant. The initial runtime AI adapter is OpenAI, behind replaceable interfaces.
Use server-side typed HTTP clients or an official SDK that supports the selected current endpoint.
Check current official endpoint/model documentation during M0/M3 and record the capability matrix in progress.md.
Configure model IDs. Never assume all transcription models supply timestamps or accept the same response format.

Shared Web/Worker configuration contract:

    ConnectionStrings:DefaultConnection
    Media:StorageRoot
    Media:MaxUploadBytes
    Media:FfmpegPath
    Media:FfprobePath
    Ai:Mode
    Ai:Provider
    Ai:ApiKey
    Ai:StoryModel
    Ai:TranscriptionModel
    Ai:MaxCallsPerProject
    Ai:RequestTimeoutSeconds
    Jobs:MaxAttempts
    Jobs:LeaseSeconds

Default Ai:Mode is Manual. Initial suggested caps: 20 AI calls/project, 60-second timeout, 3 job attempts.
Keep credentials in user secrets/environment configuration; normal double-underscore environment mapping applies.
Web and Worker use the same connection/storage/secrets configuration.
Live with missing settings reports a clear failure and stays Live; never substitute Fixture silently.
Do not automatically repeat an ambiguous paid request without considering duplicate cost.

### Proposals
Use schema-constrained JSON where supported and independently validate it server-side.
Schema adherence does not establish source fidelity or factual accuracy.
Responses and Chat Completions APIs have different structured-output parameter shapes; follow the selected API's actual documentation.

A story proposal contains schema version, questions, proposed scenes, source references, wording/origin and missing information.
A production proposal contains resource references, shots, coverage gaps and unresolved constraints.
Accept no model-supplied executable commands, SQL, HTML, file paths or FFmpeg expressions.

Validate source IDs against the authorised project; quote text against the reviewed transcript;
timing against real audio; and resources against the supplied set.
Copy protected quotations from approved source records rather than trusting the model to reproduce them.
Keep the prior version when validation fails and allow manual correction/retry.

### Speech and language
Use timing from a verified provider, a validated aligner, or an explicit human timing action.
Text-only transcription is NeedsTiming. Provide source playback, mark-in/mark-out and confirmation.
Never infer real quote timing from text length or ask a language model to invent offsets.
ASR is unreviewed until corrected/approved. Changing text does not change spoken audio.
New spoken narration needs a new real recording in the MVP.
Translations remain drafts until a fluent person reviews them.
Preserve original text/audio and report only language quality actually tested, including mixed-language samples.

### Untrusted content
Recordings, transcripts and documents are quoted data, not operational instructions.
They cannot change system rules, access another project, invoke app tools or authorise publication.
Send only the authorised necessary project context to providers.
Log request IDs, usage/cost if supplied and failure categories, not keys or complete private transcripts.

## 6. Durable jobs

Use a SQL queue consumed by Worker. Do not render inside HTTP requests or unobserved Task.Run calls.
States: Queued, Running, Succeeded, Failed, Cancelled.
Store next-attempt time, current owner/lease and an actionable last failure category.
An atomic claim prevents two workers owning one attempt; heartbeat/renew the lease.
Each attempt has a unique lease/attempt token. Completing output must atomically match that token,
the current job state and snapshot/permission checks. A worker that loses its lease aborts child work
and cannot publish output; a stale worker may not overwrite a reclaimed attempt's result.
Expired work is reclaimable within bounded attempts. Handlers are idempotent.
Deduplicate by kind, project and immutable target hash.

Keep partial output private, publish only after validation, and clean failed temporary files.
Propagate cancellation to provider calls/subprocesses.
Recheck permissions before execution and before completing output.
A worker restart must preserve queued work.

## 7. Real rendering

A server-built immutable RenderManifest contains schema version/hash, ordered scenes and durations,
safe private keys, crop choices, source hashes/revisions and actual approved audio ranges,
captions mapped onto OUTPUT time, approved titles/credits, permissions and output settings.

Readiness rejects missing visuals, unrecorded proposed narration, invalid ranges, stale approvals,
withdrawn permissions, unsupported media and impossible duration.
Short cuts may preview; the hackathon final export must be 60–90 seconds.
Do not pad with invented speech to meet duration.

Process:
1. Probe and normalise approved inputs.
2. Apply declared crop/letterbox choices and original-audio trims with compatible media formats.
3. Assemble scene order; map caption cues from source edits onto output time.
4. Render approved captions/credits and create MP4 plus SRT.
5. Probe the actual output, verifying streams, dimensions and duration before success.

Target: 1080 x 1920, 30 fps, H.264/AAC, yuv420p and fast-start metadata when supported by the selected build.
A 540 x 960 preview is allowed and explicitly labelled.
Use ProcessStartInfo.ArgumentList, UseShellExecute=false, controlled working directories and bounded logs/timeouts.
Restrict inputs to controlled local files/protocols. Never interpolate user/model text into shell commands or arbitrary filters.
Encode title/caption content as data; test apostrophes, colons, brackets, newlines and non-ASCII text.
Check subtitle font coverage; missing glyphs block the affected language.
The MVP uses deterministic cuts/fades and approved photos/video, not generated historical imagery.

## 8. Revisions and approval integrity

Readiness binds to actual media hashes, transcript/intent/story/plan revisions, captions, credits and permissions.
Hash a canonical snapshot. Approved snapshots are immutable.
Any relevant edit creates a new revision and invalidates affected approvals.
Use concurrency checks during export creation/execution and current permission checks on download.
An old render cannot appear as the newly approved current cut.
Reject stale current-export downloads; design historical artifact access later.

## 9. Preservation ZIP

Export originals, reviewed transcripts with timing, creator context, story/plan data, captions, credits and film.
Provide a versioned JSON manifest, readable index, file checksums and honest content-origin labels.
Exclude API keys, access tokens, private server paths and unnecessary contact details.
Use safe entry names and bounded streaming. Permissions govern which media can be included.
Portable records/copies are the preservation mechanism, not a guarantee of permanent hosting or copyright certification.

## 10. Verification and operation

Use xUnit or the existing repository framework.
Unit-test invariants; integration-test SQL migrations/leases, storage and FFmpeg against actual dependencies.
Browser-test the creator route and two-owner isolation.
Paid provider tests are opt-in, bounded and require real configuration.
Use secure framework authentication cookies, antiforgery protection, validation and private downloads.
Document actual migration/startup/Worker/FFmpeg/browser-test commands as verified.
