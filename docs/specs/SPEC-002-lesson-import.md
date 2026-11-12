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
- **Only CC BY videos** can be imported for now. Before fetching any captions, the video's licence is checked with the official YouTube Data API (`videos.list`, `status.license` must be `creativeCommon`, which on YouTube is always CC BY 3.0). Standard-licence videos are rejected and no captions are fetched or stored. See [ADR-002](../decisions/ADR-002-caption-source.md).
- Videos that can't be embedded (`status.embeddable: false`) are rejected too, because practice uses the embedded player (SPEC-003).
- Captions come **only from YouTube** (English). How we fetch them is decided in [ADR-002](../decisions/ADR-002-caption-source.md): YoutubeExplode behind an `ICaptionSource` interface, through a forward proxy. Manual English captions are preferred; auto-generated ones are accepted.
- **Stored with each video, as attribution and evidence:** licence (`CC BY 3.0`), title, channel name and ID, video URL, and the date the licence was checked. A CC BY licence can't be withdrawn for copies made while it applied, so the licence isn't re-checked later; removal on request still goes through the takedown process.
- **Attribution is shown wherever captions are shown** (lesson list and dictation screen): video title, channel name, link to the video, `CC BY 3.0` linked to the licence, and "Captions segmented by Dictora" (CC BY requires saying the work was changed).
- If the same video was already imported by anyone, reuse the stored captions (no refetch). Each user still gets their own lesson.
- A lesson belongs to one user. Users can't see each other's lessons.
- Errors, each with its own error code and message: invalid URL, video unavailable (private/deleted/age-restricted/live/not embeddable), not CC BY licensed, no English captions.
- Segments:
  - Raw caption cues are merged or split into **sentences**, each with a start and end time.
  - Auto-generated captions often have little punctuation, so fall back to splitting on pauses or length.
  - Target segment length is about 2–15 s. The exact rules are written down next to the code.

## Acceptance criteria

### Import API (#11)
- [ ] `POST /lessons { url }` accepts all URL forms listed above
- [ ] Checks the licence with the YouTube Data API first; only `creativeCommon` (CC BY) and embeddable videos continue
- [ ] Fetches captions using the approach in [ADR-002](../decisions/ADR-002-caption-source.md)
- [ ] Stores video metadata (id, title, duration, thumbnail), attribution (licence, channel name and ID, URL, licence-check date) and raw caption cues with timestamps
- [ ] Reuses stored captions for an already-imported video
- [ ] Returns distinct error codes for invalid URL, video unavailable, not CC BY licensed, and no English captions
- [ ] The lesson belongs to the current user

### Segmentation (#12)
- [ ] Cues are merged/split into sentences with start/end times
- [ ] Auto-generated captions fall back to pause- or length-based splitting
- [ ] Segment length kept within the target range; rules documented
- [ ] Unit tests using real caption samples from spike #2

### UI (#13)
- [ ] URL input + submit, with a loading state while importing
- [ ] Friendly message for each API error code (`en` + `vi`)
- [ ] "My lessons" list: thumbnail, title, progress, last practiced, and the CC BY attribution
- [ ] The "not CC BY licensed" message explains why and how to find CC BY videos (YouTube search filter: *Creative Commons*)
- [ ] Clicking a lesson opens the dictation screen

## Out of scope
- Videos with a standard YouTube licence (revisit after a legal review)
- Speech-to-text for videos without captions
- Languages other than English for dictation content
- Editing segments by hand
- Deleting or sharing lessons
