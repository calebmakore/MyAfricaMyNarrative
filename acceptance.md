# Acceptance scenarios

Require observed evidence. Use these IDs in tests and progress.md.
Some checks need real SQL Server, FFmpeg, browser automation, configured APIs or a fluent reviewer.

## AC01 — Ownership
Users A/B cannot list/read/change each other's projects, source files, jobs or exports.
Test direct ID/URL requests, mutations, playback, polling and downloads.

## AC02 — Uploads and originals
A supported file is privately stored, probed, playable and persistent after refresh.
Its original hash does not change during processing.
Corrupt/oversized/false-type files and traversal-like filenames fail safely.
App/host upload limits match the documented configuration.

## AC03 — Recording
A compatible microphone recording saves/plays.
Permission refusal and unsupported formats provide a working upload fallback.
Navigation cannot discard unsaved audio silently.

## AC04 — Transcription/timing honesty
Live output is real provider output with provenance.
Text-only output is NeedsTiming, not invented precise timestamps.
Manual mark-in/out uses actual playback and bounded confirmed ranges.
Manual/Fixture cannot masquerade as Live; provider failure preserves reviewed work.

## AC05 — Reviewed sources
Transcript corrections create immutable revisions.
Each quotation references reviewed text plus actual source audio/timing.
View source plays the corresponding segment.
Edited text never falsely implies the speaker recorded a generated replacement.

## AC06 — Intention/proposals
Audience, meaning, tone, local context and protected terms persist.
Proposals can be compared/applied/rejected; rejection preserves current work.
Skipped questions remain missing context rather than invented facts.

## AC07 — Quote and prompt safety
Generation/replanning preserves protected words.
Foreign/nonexistent source IDs, changed quotations and invalid ranges are rejected.
Instructions embedded in source material cannot expose data or change workflow.
Valid JSON alone cannot bypass semantic checks.

## AC08 — Resources
Plans use only declared equipment, people, locations and availability.
Removing a resource yields an explicit comparison before applying.
Unknown resources/prices and infeasible timings are flagged.
Planned footage remains missing until uploaded or replaced by an approved available visual.

## AC09 — Language
Original terms and creator explanations survive revisions.
Translations stay drafts until real fluent review is recorded honestly.
Check selected language glyphs in UI, SRT and rendered output.
Fixture text alone is not proof of language accuracy.

## AC10 — Permissions
Selected sources have current recorded use permission and credits.
Owner attestation is distinguished from independent confirmation.
Withdrawal blocks new use/current downloads and cancels affected pending work.
The app makes no claim that it can recall offline copies.

## AC11 — Revisions/concurrency
Approval binds to the exact immutable snapshot.
Material changes to words, ranges, media, context, captions, credits or constraints invalidate readiness.
Concurrent edits surface a conflict.
An older render cannot become the approved current film after such an edit.

## AC12 — Durable jobs
Queued work survives a host restart.
Two workers cannot own one attempt at once.
Expired leases recover within bounded attempts.
A worker with an old attempt token cannot publish after another worker reclaims the job.
Duplicate enqueue yields one logical successful artifact per target.
Cancellation stops child work and cleans partial output.

## AC13 — Actual film
A current approved 60–90 second 1080 x 1920 MP4 plays/downloads.
ffprobe confirms actual video/audio streams, dimensions and duration.
The audio uses the approved original recording.
Downloads return authorised actual bytes.
Missing renderer/codec/media/approval yields an actionable failure, not a fake successful export.

## AC14 — Captions
Trim/reorder maps cues onto output time.
Cues are ordered, bounded and match spoken selections.
Approved text/glyphs render correctly.
Apostrophes, brackets, colons and newlines cannot become commands or break rendering.

## AC15 — Preservation ZIP
The archive contains usable originals, reviewed transcripts/timing, context, credits, captions and film.
Schema version/checksums/references are valid.
No keys, tokens or private server paths are present.
Records can be understood without the application database.
Withdrawn/unapproved assets are excluded or block the requested export.

## AC16 — Mobile usability
The complete route works at approximately 360 px and 390 px, tablet and desktop.
No horizontal overflow or essential hover-only action.
Keyboard, errors, focus and touch targets are usable.
Refresh/back/reopen retain saved work.
Every visible core action works or gives an explicit disabled reason.

## AC17 — Honest Live demo
When configured, demonstrate real transcription and story/resource calls.
Record which steps are Live/Manual/Fixture.
Missing keys or fluent review remain visible blockers to those claims.
Demo sources are consented or visibly synthetic.

## AC18 — Handover
Verified commands restore/build, migrate a fresh dedicated database and start Web/Worker.
Dependency/configuration instructions contain no credentials.
Relevant unit, SQL, renderer and browser checks have actual results.
No deployment, private-media publishing, external messaging or merge happens by default.

## Check strategy

| Concern | Fast check | Boundary evidence |
| --- | --- | --- |
| Quote/source integrity | Changed text/foreign ID cases. | Play the actual approved source segment. |
| Resources | Validate against a fixed allowed set. | Change availability through the UI. |
| Permissions | Denied/withdrawn service cases. | Two-user direct URL/download checks. |
| Captions | Output timeline mapping cases. | Inspect real SRT and rendered glyphs. |
| Rendering | Tiny labelled synthetic fixture. | Approved original-voice 60–90 second film. |
| Jobs | SQL claim/retry/cancel cases. | Restart and competing workers. |
| Language | Preserved text and font coverage. | Real fluent review of pilot material. |

Use dedicated SQL Server or an explicit SQL Server test container.
EF InMemory/SQLite does not prove SQL Server migrations/concurrency.
Paid tests remain opt-in/bounded; fixture checks do not replace the Live evidence.
