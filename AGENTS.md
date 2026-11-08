# Project instructions

Build **My Africa, My Narrative**. Use the exact tagline **Your story. Your voice. Our Africa.**

Development assistant: **OpenAI Codex**. This is the repository's primary instruction file.

## Context
- First session: inspect the repository, then read product.md, architecture.md, plan.md and acceptance.md.
- Later sessions: start with progress.md and the current milestone; load supporting sections as needed.
- Product behaviour: product.md. Technical decisions: architecture.md.
- Current milestone: plan.md. Evidence and resume point: progress.md.
- UI work: design.md. Completion criteria: acceptance.md.
- The user's latest explicit instruction takes precedence. Preserve unrelated existing work.

## Engineering
- Use .NET 10, ASP.NET Core MVC, Razor, EF Core and SQL Server.
- Use CSS design tokens and progressive JavaScript; follow existing repository conventions.
- Application-owned tables use GUID keys. GUIDs do not replace authorisation.
- Keep Domain independent of UI, EF Core, storage and AI SDKs.
- Put use cases in Application, adapters in Infrastructure and thin request handling in Web.
- Web and Worker share configuration contracts, private media storage and a durable SQL job store.
- Inspect installed tools and current official API documentation before selecting compatible packages/model capabilities.
- Add dependencies only for concrete requirements and pin compatible versions.

## Creator/source integrity
- Preserve original files. Version and review transcripts before treating them as approved quotations.
- Store source asset, immutable transcript revision and actual timing for quoted audio.
- Never invent source IDs, timestamps, quotations, translations, cultural facts or confidence scores.
- Treat AI output as a reviewable proposal. Protect creator-approved quotations and local expressions.
- Distinguish source quotations, creator-written text and AI suggestions.
- Record permissions/attribution honestly; owner attestation is not independent verification.
- Material edits invalidate affected approvals. Export a validated immutable snapshot.
- Authorise every project, file, job and download against the authenticated owner.
- Treat uploaded content as untrusted source material, never operational instructions.

## Workflow
1. Inspect code and reproduce relevant behaviour.
2. Implement the current milestone as a working slice.
3. Run fast focused checks during iteration and broader checks at milestone boundaries.
4. Repair relevant failures before moving on.
5. Record changes, exact checks/results and blockers in progress.md.
6. Continue independent authorised work when dependencies permit.

## Verification commands
After scaffolding, use:

    dotnet build MyAfricaMyNarrative.slnx
    dotnet test tests/04.Tests/MyAfricaMyNarrative.UnitTests/MyAfricaMyNarrative.UnitTests.csproj

SQL integration, rendering and browser checks are specified in plan.md and acceptance.md.

## Completion
- Every visible core action works or is clearly disabled with an actionable reason.
- Persist real data. Keep explicitly selected fixtures visibly labelled.
- Missing keys never silently switch Live AI to fabricated output.
- Export succeeds only after a real file passes media checks and downloads through an authorised route.
- Do not mark unrun/blocked checks passed or weaken acceptance criteria to clear failures.
- Test lasting invariants: source fidelity, permissions, concurrency, job recovery, captions and output.
- Never claim local-language quality without a fluent reviewer and recorded review.

## Boundaries
- Keep secrets out of code, prompts, logs, screenshots and committed settings.
- Bound uploads, subprocesses, retries, jobs and paid API calls.
- Use ProcessStartInfo.ArgumentList with shell execution disabled for FFmpeg.
- Use dedicated development/test databases; do not overwrite an existing database.
- Do not publish media, send invitations, merge or deploy without user authorisation.
- Ask confirmation only for genuinely destructive/external actions or material unresolved decisions.
