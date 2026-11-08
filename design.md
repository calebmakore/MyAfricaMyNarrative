# Design and interactions

## Direction

A contemporary editorial studio. Give the creator's media and words priority.
Use specific creator-supplied places and context rather than a universal “African aesthetic”.

| Token | Suggested value |
| --- | --- |
| Canvas | Warm ivory #F7F2E8 |
| Text/navigation | Deep ink green #14261F |
| Primary accent | Ochre #D9A74A with dark text |
| Secondary accent | Clay #C45D3C |
| Surface/border | #FFFFFF / #DDD8CE |
| Type | System sans-serif controls; restrained Georgia story titles |
| Spacing | 4, 8, 12, 16, 24, 32, 48 px |
| Cards | 12–16 px corners, restrained shadows |

Check actual colour pairs against WCAG AA. Do not assume accent colours work as small text.
Use system fonts first; record licensing for downloaded fonts.

## Navigation

Projects dashboard plus four studio destinations: Record, Shape, Make, Export.
Mobile: compact accessible bottom navigation and large touch targets.
Desktop: left navigation, central workspace and contextual preview/source panel.
Stack panels cleanly at narrow widths.

## Screens

### Projects
Show real projects, useful status and one prominent start action.
Provide an honest empty state and labels for sample projects.

### Record
Make recording and uploading discoverable.
Provide duration, pause/stop/save, permission refusal, upload fallback and media limits.
Warn before discarding an unsaved recording.
Show actual source playback, attribution and recorded permission status.

### Shape
Combine a player and transcript; stack them on phones.
Provide accessible mark-in/mark-out, play selection and timing confirmation.
Use wording such as “From your recording”, “Written by you” and “AI suggestion”.
Show how caption correction relates to original spoken audio.
Scene reorder works with touch and keyboard.
Protected words have a visible state and intentional edit action.

### Make
Use short resource forms and scene-linked shot cards.
Show missing footage and available alternatives.
Changed constraints create a reviewable proposal, not silent changes.
A preview identifies its revision and whether it needs rebuilding.

### Export
Show a readiness checklist linked to fixes.
Examples: review a subtitle, confirm a photo's permission, record a line, add source timing.
Show actual queued/running/failed/cancelled/completed states and progress only when measurable.
Offer playback and real film/caption/archive downloads after success.

## Behaviour
- Save meaningful edits and display saved/saving/failed states.
- Preserve values after validation failure; explain concurrency conflicts.
- Compare proposed AI changes before applying.
- Keep stack traces, model/token details and queue internals out of normal creator screens.
- Show Manual/Live/Fixture status where necessary to prevent a misleading demo.
- Use accessible validation, visible focus and reduced motion.
- Essential actions cannot depend only on hover, colour, drag or an icon.
- Provide an accessible alternative to waveform-only timing.
- Disabled controls explain the next useful action.
- Avoid autoplaying audio; use restrained feedback for saves and progress.
