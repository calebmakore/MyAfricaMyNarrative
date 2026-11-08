# Sources

Prepared: 2026-11-08. Recheck changing API/package capabilities during implementation.

## Product inputs
- The user's supplied OpenAI/Andela hackathon brief: Creation & Craft, with a working proof of concept.
- The agreed concept, preservation purpose, submission description and final tagline.
- Technical architecture is a proposed implementation, not evidence of a completed application.

## Official technical references

| Topic | Reference | Application |
| --- | --- | --- |
| Codex project instructions | https://learn.chatgpt.com/docs/agent-configuration/agents-md | Root AGENTS.md provides persistent project guidance; the prompts name supporting documents to read. |
| EF SQL Server | https://learn.microsoft.com/en-us/ef/core/providers/sql-server/ | Supported provider and compatible inspected versions. |
| EF Core 10 | https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew | Current target generation; inspect exact patch dependencies. |
| File transcription | https://developers.openai.com/api/docs/guides/speech-to-text | Verify actual language, format and timing support. |
| Transcription API | https://developers.openai.com/api/reference/resources/audio/subresources/transcriptions/methods/create | Match request options to the selected model. |
| Structured output | https://developers.openai.com/api/docs/guides/structured-outputs | Constrained proposals plus independent semantic validation. |
| FFmpeg | https://ffmpeg.org/ffmpeg.html | Real bounded local media rendering. |
| ffprobe | https://ffmpeg.org/ffprobe.html | Verify actual source/output metadata. |

The fetched speech guide recommends gpt-transcribe for general transcription and uses some legacy timestamp examples.
This pack intentionally does not pin a transcription model or promise its timestamps.
Inspect the chosen model's actual capabilities and retain explicit human timing.

## Research and existing tools

- UNESCO, African film-industry report (2021), overview:
  https://www.unesco.org/en/articles/unesco-spotlights-growth-potential-africas-film-industry
  Background on training/infrastructure; not a current market-size estimate.
- AfricaNLP 2025, African low-resource speech recognition:
  https://aclanthology.org/2025.africanlp-1.13/
  Language capability requires real evaluation.
- Descript: https://www.descript.com/transcription
- StudioBinder: https://www.studiobinder.com/storyboarding-tool/

The hypothesis to validate is the usefulness of a simple mobile workflow combining creator context,
resource constraints, source fidelity and practical production support.
Existing tools already cover substantial editing/planning functionality.
Do not claim global uniqueness, assured awards or untested cultural/language accuracy.
