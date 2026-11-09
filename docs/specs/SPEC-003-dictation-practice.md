# SPEC-003: Dictation practice

- **Status:** Draft
- **Milestone:** M1 MVP
- **Issues:** #14 (player), #15 (answer checking), #16 (progress)
- **Related:** SPEC-002 (where segments come from)

## Goal
As a signed-in user, I listen to a lesson one sentence at a time, type what I hear, see exactly what I got right or wrong, and can come back later to continue.

## User flow
1. The user opens a lesson. It resumes at the first unfinished segment.
2. The current segment plays once. The transcript is hidden.
3. The user replays as often as they like (optionally at a slower speed), types the sentence, and checks it.
4. They see a word-by-word result and a score for the segment, or they can reveal the answer.
5. They move to the next segment. Progress is saved after each one.
6. At the end (or any time), a summary shows their accuracy and progress.

## Rules and edge cases
- The video plays in the embedded YouTube player (IFrame API), limited to the current segment's start → end.
- Answer checking compares **normalized** text:
  - case-insensitive and punctuation ignored
  - contractions and numbers handled consistently (e.g. `don't` / `do not`, `5` / `five`). The exact rules are documented with the code.
- The word-level diff marks each word as **correct**, **wrong**, **missing**, or **extra**.
- A revealed segment counts as attempted, not correct.
- Progress is per user, per lesson.

## Acceptance criteria

### Player (#14)
- [ ] Embedded YouTube player plays only the current segment's start → end
- [ ] Controls: replay, previous/next segment, playback speed (0.75× / 1×)
- [ ] Keyboard shortcuts (e.g. Ctrl+Space to replay, Enter to check)
- [ ] Text input for the answer; transcript hidden until checked or revealed
- [ ] All text in `en` + `vi`

### Answer checking (#15)
- [ ] Normalization rules implemented and documented
- [ ] Word-level diff shows correct / wrong / missing / extra
- [ ] Per-segment score; "reveal answer" option
- [ ] Decide where checking runs (client, server, or shared rules) and record it if it's non-obvious
- [ ] Unit tests for normalization and diff edge cases

### Progress (#16)
- [ ] Per-segment attempts and results stored per user
- [ ] Opening a lesson resumes at the first unfinished segment
- [ ] Lesson summary: accuracy %, segments completed, time spent
- [ ] Progress shown in "My lessons" (SPEC-002)

## Out of scope
- Streaks, leaderboards, spaced repetition
- Hints (e.g. showing the first letter of each word)
- Mobile-specific layout beyond basic responsiveness
