# My Africa, My Narrative — guide for judges

**Your story. Your voice. Our Africa.**

A private, phone-first studio that turns a voice note, a few photos or a short video into a 60–90 second documentary the storyteller directs — with their words kept exactly as they said them.

Track: **Creation & Craft** (with strong links to Culture & Heritage).

## The problem

Across Africa, powerful stories live in voice notes, notebooks and family photos. Turning them into a film usually means handing your words to someone else, and AI tools make it easy to put words in people's mouths. Storytellers need a tool that helps them make something polished **without losing ownership of what they said**.

## What it does — the four steps

| Step | What the storyteller does |
|---|---|
| **Record** | Record a voice note or a video (sound and picture together) in the browser, make a title slide from your own words, or upload audio, photos and clips. Every source is private; who supplied it and whether they agreed is recorded. |
| **Shape** | Listen and type exactly what was said. Build scenes that are real clips of the recording, labelled as quotes, own words or AI-suggested words. Protect expressions that must stay as spoken, and explain local words once. Combine several pictures under one stretch of voice, start a video where you choose, or keep its own sound quietly underneath. A story coach finds the missing piece. |
| **Make** | “Make it with what I have”: declare the phone, place, people and time you really have, and get a shoot plan that fits. When someone drops out, the new plan appears beside the old one with exactly what changed. |
| **Export** | A plain-language checklist, then one click makes a vertical 1080 × 1920 MP4 with burned-in captions and a credits card, in the background with live progress. A preservation archive keeps the originals, words and film for the family. |

## What makes it different

1. **Source fidelity is enforced, not promised.** A scene marked “From my recording” can only quote checked words from that recording, inside the checked time range. Any edit makes a new immutable version and needs re-approval. The export is bound to that exact approved version.
2. **AI suggests; the storyteller decides.** Shoot plans, story coaching, transcription and word finding are labelled proposals, validated against the real project (no invented scenes, people, pictures or words) and changed only when the storyteller approves. If AI drafted any words on screen, the film's credits say so.
3. **Culture stays intact.** Protected expressions must survive into captions; a creator-approved explanation appears under the first caption where a local word is spoken. Language quality is never claimed without a named, fluent reviewer.
4. **Made for real conditions.** Works on a phone, plans around what you actually have, and keeps working when the AI service is down (free practice versions of every AI feature, and plain-language messages when the service is busy or out of credit).
5. **Evidence, not claims.** Insights measure time to an approved cut, how much AI help was kept, and whether the film “says what you meant”, with a JSON download for pilot facilitators (no media or story words included).

## Try it

- Local: see `docs/demo-runbook.md` (SQL Server, FFmpeg, then `dotnet run` for the web app — rendering runs inside it).
- Demo account: `calebtest@myafricamynarative.com` (password supplied separately). It holds:
  - **I Dream for an Africa** — a complete story: the creator's own spoken-word poem, voice and music, six original artworks, an 84.5 s final film.
  - **To Rebuild Africa — live performance (in progress)** — deliberately unfinished: a shoot plan that changed when a helper dropped out, coach questions, a word to explain, and an export checklist showing what is left.
- Submission video: `demo-media/video/My-Africa-My-Narrative-demo-caleb-voice.mp4`, narrated by the creator.

## How it is built

.NET 10, ASP.NET Core MVC with Razor, EF Core and SQL Server; FFmpeg for rendering; OpenAI Responses API with strict JSON schemas for the optional Live AI features. Layered Domain / Application / Infrastructure / Web; a durable SQL job store renders films in the background (row-locked claims, renewed leases, attempt tokens), hosted by the web app or an optional Worker. Progressive JavaScript only; CSS design tokens; installable on phones (web app manifest); security headers including a strict content security policy.

## Verified (see `progress.md` for exact commands and results)

- 84 unit tests; 8 integration tests (job safety against real SQL Server — racing processors, lease renewal, lost leases, crash recovery — and real FFmpeg rendering).
- Final film checked with ffprobe: H.264/AAC, 1080 × 1920, 84.5 s.
- Every page reviewed at phone (390 px) and desktop widths.
- In-browser video recording tested end to end with a synthetic camera: recorded, saved as “video with sound”, used as a scene's voice and picture, rendered, and the picture checked to start in sync with the voice.

## Honest limitations

- Live AI features were demonstrated with real calls during development; the demo was recorded while the OpenAI account was out of credit, so the video shows the free practice versions.
- No fluent reviewer for languages other than English has reviewed the demo; the app will not claim local-language quality without one.
- Pilot measures come from the demo account only; a pilot with 3–5 real creators is the next step.
