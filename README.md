# My Africa, My Narrative

**Your story. Your voice. Our Africa.**

An ASP.NET Core MVC studio for creating a private, source-backed short documentary while preserving original voice, permission evidence, reviewed timing and captions.

See [docs/demo-runbook.md](docs/demo-runbook.md) for verified local setup, migrations, host/Worker startup, diagnostics and the final demonstration path. Current implementation evidence and any unrun boundaries remain in [progress.md](progress.md).

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

A creator records/uploads sources, reviews transcript/timing, protects words, arranges scenes,
changes available resources, approves the cut and downloads a real 60–90 second vertical film.
They can keep originals, reviewed transcripts, context, captions and credits in a portable archive.
Use [acceptance.md](acceptance.md) to assess this behaviour.

Keep long rationale in architecture.md and actual evidence in progress.md.
AGENTS.md stays short enough to remain useful.
