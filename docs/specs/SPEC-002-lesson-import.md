# SPEC-002: Lesson import

- **Status:** Draft
- **Milestone:** M1 MVP
- **Issues:** #11 (import API), #12 (segmentation), #13 (UI)
- **Related:** [ADR-002](../decisions/ADR-002-caption-source.md), SPEC-001, SPEC-003

## Goal
As a signed-in user, I paste a YouTube URL and get a lesson: the video's English captions split into sentence-level segments I can practice.

## User flow
1. On **New lesson**, the user pastes a YouTube URL and submits.
2. They see a loading state while the video is imported.
3. On success, they're taken to the lesson. On failure, they see a friendly message explaining why.
4. **My lessons** lists all their lessons. Clicking one opens it in the dictation screen (SPEC-003).

## Rules and edge cases
- Accepted URL forms: `youtube.com/watch?v=`, `youtu.be/`, `youtube.com/shorts/`, with or without extra parameters (`t=`, `list=`, …).
- Captions come **only from YouTube** (English). How we fetch them is decided in [ADR-002](../decisions/ADR-002-caption-source.md): our own thin client, through a forward proxy. Manual English captions are preferred; auto-generated ones are accepted.
- If the same video was already imported by anyone, reuse the stored captions (no refetch). Each user still gets their own lesson.
- A lesson belongs to one user. Users can't see each other's lessons.
- Errors, each with its own error code and message: invalid URL, video unavailable (private/deleted/age-restricted/live), no English captions.
- Segments:
  - Raw caption cues are merged or split into **sentences**, each with a start and end time.
  - Auto-generated captions often have little punctuation, so fall back to splitting on pauses or length.
  - Target segment length is about 2–15 s. The exact rules are written down next to the code.

## Acceptance criteria

### Import API (#11)
- [ ] `POST /lessons { url }` accepts all URL forms listed above
- [ ] Fetches captions using the approach in [ADR-002](../decisions/ADR-002-caption-source.md)
- [ ] Stores video metadata (id, title, duration, thumbnail) and raw caption cues with timestamps
- [ ] Reuses stored captions for an already-imported video
- [ ] Returns distinct error codes for invalid URL, video unavailable, and no English captions
- [ ] The lesson belongs to the current user

### Segmentation (#12)
- [ ] Cues are merged/split into sentences with start/end times
- [ ] Auto-generated captions fall back to pause- or length-based splitting
- [ ] Segment length kept within the target range; rules documented
- [ ] Unit tests using real caption samples from spike #2

### UI (#13)
- [ ] URL input + submit, with a loading state while importing
- [ ] Friendly message for each API error code (`en` + `vi`)
- [ ] "My lessons" list: thumbnail, title, progress, last practiced
- [ ] Clicking a lesson opens the dictation screen

## Out of scope
- Speech-to-text for videos without captions
- Languages other than English for dictation content
- Editing segments by hand
- Deleting or sharing lessons
