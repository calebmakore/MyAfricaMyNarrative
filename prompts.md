# OpenAI Codex prompts

Keep these files in the repository root. Start with Prompt 1.
Use numbered prompts in order or the longer continuous-build prompt after setup.
Development prompts do not authorise production changes, public publishing or external messages.

## Prompt 1 — Inspect and start

    Build My Africa, My Narrative.
    Exact tagline: Your story. Your voice. Our Africa.

    Read AGENTS.md, product.md, architecture.md,
    plan.md, acceptance.md and progress.md. Read design.md before UI work.
    Inspect the actual repository and existing code before making changes.

    Implement M0 and then M1. If progress.md records completed work, verify its evidence
    and continue from the earliest incomplete relevant milestone rather than restarting.
    Use the specified .NET 10 MVC/Razor,
    EF Core and SQL Server architecture. Pin inspected compatible versions.
    Keep credentials outside Git and preserve unrelated work.

    Start implementation after inspection; do not stop at a proposed plan.
    Make routine decisions and continue without asking repeatedly to proceed.
    Ask only for a material decision or genuinely missing required information.
    If a dependency/key is missing, complete independent work and record the blocker.

    Verify behaviour using fast relevant checks and milestone checks.
    Update progress.md with changes, exact checks/results and next steps.
    Do not mark unrun/blocked checks passed or claim the application is complete.

## Prompt 2 — Real manual export

    Read AGENTS.md, progress.md, architecture.md, acceptance.md and M2 in plan.md.
    Implement M2 against the actual code.

    Build manual transcript correction/timing, source playback and scene editing.
    Preserve originals and immutable reviewed source references.
    Bind approvals/readiness to exact snapshots and current permissions.
    Implement durable SQL jobs, the Worker and actual FFmpeg rendering.

    Produce a real tiny technical export using labelled synthetic media;
    verify an approved original-voice cut when suitable material is available.
    Map captions onto output time.
    Use safe arguments, bounded subprocesses, cancellation and private downloads.

    Run focused tests and actual ffprobe checks. Verify restart/idempotence and access.
    Update progress.md with evidence/blockers.
    A fake download, static sample video or in-memory queue is not completion.

## Prompt 3 — Real AI assistance

    Implement M3 using the current code and architecture.md.
    Verify official model/endpoint documentation and actual configuration.
    Record timestamp, response-format and structured-output capabilities.
    Keep human mark-in/mark-out review when transcription returns text without timing.

    Implement Live/Manual/Fixture modes and the real provider adapters.
    Add intention capture, focused questions and structured story proposals.
    Independently validate authorised source IDs, exact quotations and actual timing.
    Preserve protected words; let creators compare/apply/reject proposals.
    Treat uploaded content as source data, not operational instructions.

    Demonstrate bounded real calls when configured; leave missing-key checks Blocked.
    Never replace failed Live calls with fabricated successes.
    Test invalid references, malicious content, schema/refusal/error paths and preservation of saved work.
    Update progress.md with exact outcomes.

## Prompt 4 — Available-resource planning

    Implement M4 in plan.md using product.md and architecture.md.
    Add resource forms, scene-linked shot lists and visible coverage gaps.

    Demonstrate one phone, one location and limited people/time.
    Remove a person/location and propose a reviewed alternative using only remaining resources.
    Preserve intention, original narration and protected words.
    Reject unknown resources/prices and keep missing footage visible.
    A production plan must not pretend that footage exists.

    Verify the actual change/apply/reject interactions and relevant invariants.
    Update progress.md.

## Prompt 5 — Language, permissions and preservation

    Implement M5.
    Add original expressions, creator context, draft/reviewed captions and honest reviewer attribution.
    Keep caption corrections separate from recorded speech.
    Preserve culturally specific meaning supplied by the creator.

    Complete credits, permission review and withdrawal handling.
    Implement a portable archive with originals, reviewed transcripts/timing,
    context, captions, film and a versioned manifest/checksums.
    Exclude secrets, server paths and unnecessary contact details.

    Test actual source playback, protected terms, changed-revision approvals,
    withdrawn-asset gates, safe archive references and non-ASCII caption rendering.
    Record which language samples actually received fluent review.
    Update progress.md with evidence and limitations.

## Prompt 6 — Polish the complete studio

    Read design.md and implement M6 on the working application.
    Use the specified editorial design and creator media.

    Complete Record, Shape, Make and Export on phones and desktop.
    Verify recording/upload fallback, autosave, timing/source playback,
    protected words, proposal review, resource changes, readiness, progress and downloads.
    Implement useful loading, empty, retry, error, validation, disabled and conflict states.

    Inspect approximately 360 px, 390 px, 768 px and desktop layouts.
    Check keyboard use, focus, contrast and reduced motion.
    Every core visible button must work or explain why it is disabled.
    Record actual inspected interactions and remaining issues in progress.md.

## Prompt 7 — Verify and hand over

    Implement M7 and verify acceptance.md against actual running behaviour.
    Run relevant build, unit, SQL integration, renderer and browser checks.
    Fix observed high-impact failures and rerun affected checks.

    Verify a real approved 60–90 second 1080 x 1920 film
    with original narration, reviewed captions/credits and an authorised playable download.
    Verify the preservation ZIP and two-owner isolation.
    Demonstrate changed resource constraints while protected words remain unchanged.

    Prepare a short demo runbook with consented or clearly synthetic sources.
    Document actual AI calls, language reviews and user tests.
    Update verified setup commands and progress.md.
    Required blockers mean the application is still incomplete.
    Do not deploy, publish user media, send messages or merge a PR.

## Continuous build — Longer uninterrupted work

    Read AGENTS.md, progress.md, plan.md, architecture.md and acceptance.md.
    Load product.md/design.md where relevant.

    Continue from the earliest incomplete milestone whose dependencies are met.
    Implement the authorised work in order through M7.
    Do not stop after planning or for routine permission to continue.
    Preserve progress in progress.md for later sessions.

    Use fast focused checks during iteration and broader checks at milestone boundaries.
    Repair failures without weakening checks. Keep budgets/timeouts/retries bounded.
    Continue independent work around missing dependencies.
    Keep blocked Live/human-review requirements explicit.

    Stop at verified completion, a genuine blocker with no independent work left,
    a material unresolved user decision, or an external/destructive action requiring approval.
    End with actual implemented behaviour, validation evidence and exact remaining blockers.

## Resume

    Read AGENTS.md and progress.md.
    Inspect actual code and the current diff/state.
    Read the current plan milestone and its relevant technical/acceptance sections.
    Resume from saved evidence; preserve completed work.
    Do not restart the project or trust undocumented completion claims.
    Update progress.md before ending.

## Investigate a failure

    Reproduce the failure with the smallest useful input/test.
    Read the governing acceptance scenario and relevant actual call sites/logs.
    Keep secrets/private transcripts out of output.
    Explain the observed cause, apply a focused fix and rerun validation.
    Add a meaningful regression check for a lasting invariant.
    Avoid unrelated redesign or silently changing providers/requirements.

## Review only

    Review the current diff/application against AGENTS.md, architecture.md and acceptance.md.
    Do not edit files.
    Prioritise exposure, changed quotations, invented timestamps, stale approvals,
    permission bypass, duplicate/restarted jobs, unsafe FFmpeg commands,
    broken captions and fake success states.
    Report concrete findings with severity, file location, failure path and recommended correction.
    Distinguish confirmed findings from unverified concerns.

## Demo script

    Read product.md, project-description.md and actual evidence in progress.md.
    Draft a concise demo covering source recording, intention, scene/source links,
    a changed resource constraint, approved film and preservation package.
    Use the exact name/tagline.
    Label Live, Manual and Fixture steps honestly.
    Do not invent adoption, cultural accuracy, fluent review or historical verification.
    Mark any missing demonstration step as a blocker.
