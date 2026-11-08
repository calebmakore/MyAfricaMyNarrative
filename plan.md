# Implementation plan

Development assistant: OpenAI Codex.

Application status: Not started. This pack is documentation, not a completed application.

## Milestones

| ID | Outcome | Depends on |
| --- | --- | --- |
| M0 | Repository inspection, pinned dependencies, configuration and capability decisions. | None. |
| M1 | Authenticated creator, private project and playable sources. | M0. |
| M2 | Manual source-backed story produces a real approved video. | M1. |
| M3 | Live transcription, focused questions and safe story proposals. | M2. |
| M4 | Resource-aware production planning and reviewed replanning. | M3. |
| M5 | Language/context review, permissions, credits and preservation copies. | M4. |
| M6 | Complete accessible mobile/desktop studio. | M5. |
| M7 | Verified demonstration and reproducible handover. | M6. |

Build a working slice before expanding it. Put ownership, permissions and revision foundations in M1/M2.
Later milestones deepen these controls; they do not permit earlier export bypass.
Record exact evidence in progress.md. Blocked/unrun checks are not passes.

## M0 — Inspect and prepare
- Read the specifications; inspect existing code and preserve unrelated work.
- Inspect .NET, SQL Server, FFmpeg/ffprobe and browser-test dependencies.
- Scaffold the architecture.md project boundaries and MyAfricaMyNarrative.slnx.
- Pin compatible inspected .NET 10/EF versions.
- Provide placeholder configuration; share database/storage configuration between Web and Worker.
- Inspect current API/model timestamp and structured-output capabilities.
- Default to Manual mode without keys; keep Fixture explicit.
- Document actual startup/migration/test commands and precise dependency blockers.

Done when the configured solution restores/builds, startup configuration errors are useful,
and no credentials or invented capabilities appear in the repository.

## M1 — Private creator workspace
- Implement ASP.NET Core Identity, GUID users/roles and owner-scoped projects.
- Add migrations, private storage, immutable originals, hashes/probed metadata and concurrency.
- Support create/list/open/rename/archive/delete with clear outcomes.
- Implement recording in compatible secure browser contexts, upload fallback and playback.
- Enforce bounded multipart uploads and actual media validation.
- Record attribution/permission declarations, including withdrawal.
- Establish durable jobs and the Record/Shape/Make/Export navigation.

Done when refresh retains data; two users cannot access each other's project/media/job IDs;
invalid uploads fail safely; original hashes stay unchanged; microphone refusal has a working fallback.

## M2 — Real manual export early
- Implement transcript correction/manual segments, source playback and human mark-in/out timing.
- Add editable/reorderable scenes, real visual selection and original audio ranges.
- Create immutable revisions, snapshot approval and export readiness.
- Implement SQL job claims/leases, Worker execution/recovery, bounded retries and cancellation.
- Implement a real FFmpeg render, progress, preview and authorised MP4/SRT downloads.
- Re-map captions onto output time after reorder/trimming.

Done when a labelled synthetic technical fixture produces an actual verified file,
and an approved source recording demonstrates original-voice playback/trimming.
Use ffprobe to check output streams/dimensions/duration.
Prove stale-approval/permission rejection and worker restart/idempotence.
A tiny technical render is useful pipeline evidence; M7 still requires the real 60–90 second documentary.

## M3 — AI assistance
- Implement real configured provider adapters and visible Live/Manual/Fixture modes.
- Record actual endpoint/model capability evidence.
- Preserve real provider timing; keep NeedsTiming/manual confirmation for text-only output.
- Add transcript review, intention capture, focused interview questions and structured story proposals.
- Add proposal comparison/apply/reject and protection of approved quotations.
- Validate source/project IDs, timing, quote fidelity and bounded costs/retries.

Done when actual configured transcription/story calls are demonstrated and failure/rejection cases preserve work.
Test malicious transcript instructions, foreign/nonexistent IDs, altered quotations and invalid response shapes.
Missing keys remain an explicit Live-check blocker; complete independent work where possible.
Never silently replace Live failures with fixture output.

## M4 — Production within resources
- Implement equipment, people/availability, locations, filming time and optional budget.
- Generate scene-linked shots, coverage gaps and explicit missing-footage states.
- Propose and compare a changed plan when a resource changes.
- Preserve protected quotations, original intention and approved language.
- Recalculate readiness on changed story/plan revisions.

Done when removing a person/location yields a reviewed proposal using only remaining resources.
Reject invented resources/prices; missing footage cannot pass readiness.

## M5 — Language and preservation
- Add original expressions, creator explanations and draft/reviewed subtitle states.
- Record actual fluent reviewer attribution/time and test glyph/font output.
- Keep caption correction separate from recorded speech.
- Complete credits, permission review and withdrawal/deletion propagation.
- Export a real preservation ZIP with originals, transcripts/timing, context, captions, credits,
  film, versioned metadata and checksums.

Done when source links play actual segments, protected terms survive revisions,
review claims are evidenced, withdrawn material is blocked, and archives have valid references/files
without secrets or local server paths.

## M6 — Mobile studio
- Apply design.md to the complete workflow.
- Persist all meaningful edits with saved/failed/conflict states.
- Complete keyboard-accessible recording/editing/timing/source/resource/player controls.
- Implement real loading, empty, offline/retry, validation, disabled and conflict states.
- Distinguish preview, current render and stale output.

Done when the full route works at approximately 360 px, 390 px, 768 px and desktop widths,
with no horizontal overflow, essential hover-only controls or dead core buttons.
Observe back/refresh/reopen persistence, focus/contrast and reduced motion.

## M7 — Verify and hand over
- Work through acceptance.md against actual code and running behaviour.
- Fix critical failures and rerun affected checks.
- Prepare a consented source-backed demo, or clearly label synthetic material.
- Document raw recording → intent → source-backed scenes → changed constraint → film → archive.
- Record actual AI-tool usage, tested language capabilities and tested/blocked features.
- Document fresh-checkout setup, database migrations, host/Worker startup and dependency diagnostics.

Done when an approved original-voice film of 60–90 seconds at 1080 x 1920 plays/downloads,
the archive is usable, ownership checks pass, relevant checks have evidence,
and a fresh configured checkout follows verified instructions.
Any required blocker means the project is still incomplete.
Do not deploy, publish private media, send messages or merge without explicit authorisation.

## Verification cadence

Use fast focused checks during iteration and appropriate broader checks at boundaries.
Run the full relevant suite at M7 rather than rerunning slow suites after every edit.

Target commands after scaffolding:

    dotnet restore MyAfricaMyNarrative.slnx
    dotnet build MyAfricaMyNarrative.slnx --no-restore
    dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj
    dotnet test tests/04.Tests/MyAfricaMyNarrative.IntegrationTests/MyAfricaMyNarrative.IntegrationTests.csproj
    dotnet test tests/04.Tests/MyAfricaMyNarrative.BrowserTests/MyAfricaMyNarrative.BrowserTests.csproj

Integration checks need a dedicated SQL Server target and FFmpeg.
Browser checks need host/browser setup. Live tests are bounded and opt-in.
These are target commands, not claims that the application exists or passes.

## Scope control

Keep the first build focused on the original-voice documentary journey.
Add only what strengthens the creator demonstration or fixes a required correctness gap.
Record actual decisions, blockers and the next concrete step in progress.md.
