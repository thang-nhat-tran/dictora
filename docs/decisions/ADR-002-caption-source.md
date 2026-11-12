# ADR-002: Fetch YouTube captions with our own thin client

- **Status:** Accepted
- **Date:** 2026-11-12
- **Issue:** #2
- **Related:** SPEC-002, ADR-001

## Context
YouTube captions are Dictora's only text source (SPEC-002), so how we fetch them is the biggest technical risk. What matters:
- **We only need subtitles**: English caption text with start time and duration. We don't need video streams, audio, or metadata beyond what the import already stores.
- **We run on Railway.** YouTube often blocks or rate-limits datacenter IPs ("Sign in to confirm you're not a bot"), so it isn't enough for the approach to work from a laptop.
- **No official API works for us.** The YouTube Data API v3 `captions.download` call requires OAuth as the *video owner*, and our users import other people's videos.
- **YouTube changes its internals without notice.** In 2025 YouTube started returning an empty body for caption URLs taken from the watch page. That change broke the older libraries.

## Options
1. **YouTube Data API v3 (`captions`)**
   - Pros: official and stable, within YouTube's terms.
   - Cons: downloading captions needs OAuth as the video owner, so it's **unusable** for importing arbitrary videos.
2. **`vietanhdang/youtube-transcript-api-sharp`** (GitHub port of Python `youtube-transcript-api` v1.x, MIT, April 2026)
   - Pros: uses the method that works today; built-in proxy support with retries.
   - Cons: not on NuGet, one maintainer, 1 star, all written in a single day. Depending on it means copying it in or using a git submodule, and either way we'd own the fixes.
3. **yt-dlp as a sidecar** (call the `yt-dlp` binary)
   - Pros: the most robust and fastest-updated extractor; handles many edge cases.
   - Cons: adds Python and a binary to our container, starts a process per request, and needs frequent updates. Far more than we need for subtitles.
4. **Our own thin client** (about 150 lines of C#), using the same method as option 2
   - Pros: only what we need; no third-party dependency; fits our module structure, `HttpClient` setup, logging and tests; easy to route through our proxy; we understand every line.
   - Cons: we maintain it. When YouTube changes something, we have to notice and fix it ourselves; there's no upstream release to wait for.

### Comparison

| | Works today | Only subtitles | Dependency risk | Proxy support | Effort to adopt |
|---|---|---|---|---|---|
| 1. Data API v3 | ❌ (owner OAuth) | ✅ | ✅ official | n/a | n/a |
| 2. GitHub port | ✅ | ✅ | ❌ one maintainer, no releases | ✅ | Medium (copy or submodule) |
| 3. yt-dlp sidecar | ✅ | ❌ | ⚠️ needs updates | ✅ | High (container, process) |
| 4. **Own thin client** | ✅ | ✅ | ✅ none (we own it) | ✅ | Medium |

## Decision
We write **our own thin client** in the Lessons module, behind an interface (e.g. `ICaptionSource`), so it can be replaced (e.g. with yt-dlp) if it becomes too costly to maintain.

### How it fetches captions
The same method used by Python `youtube-transcript-api` v1.x:
1. `GET https://www.youtube.com/watch?v={id}` and read `INNERTUBE_API_KEY` from the HTML. If a consent page comes back, set the `CONSENT` cookie and retry. If the page contains a reCAPTCHA, we're blocked.
2. `POST https://www.youtube.com/youtubei/v1/player?key={key}` with `{"context":{"client":{"clientName":"ANDROID","clientVersion":"20.10.38"}},"videoId":"{id}"}`.
3. Check `playabilityStatus` (see the rejection rules below), then read `captions.playerCaptionsTracklistRenderer.captionTracks`. A track with `kind: "asr"` is auto-generated.
4. `GET` the chosen track's `baseUrl` with `&fmt=srv3` removed, and parse the `<text start="…" dur="…">…</text>` XML. The text is HTML-encoded, so decode it.

### Rules
- **Network:** a typed `HttpClient` that sends **every request through a forward proxy** set in configuration (URL and credentials as Railway secrets), to avoid datacenter-IP blocking. Locally the proxy is optional.
- **Choosing a track:** a manual English track (`en`, `en-*`) first, otherwise an auto-generated English track. If several match (Despacito has `en`, `en`, `en-US`), take the first `en`, then the first `en-*`.
- **Auto-generated captions are accepted** for now, despite weak punctuation and segmentation. SPEC-002 already falls back to splitting on pauses or length. We'll improve this later.
- **Rejected videos** (no dictation possible), mapped to the SPEC-002 error codes:

| Case | How we detect it | SPEC-002 error |
|---|---|---|
| Invalid or unknown video ID | `status: ERROR`, "This video is unavailable" | video unavailable |
| Private / deleted / unavailable | `status: UNPLAYABLE` | video unavailable |
| Age-restricted | `status: LOGIN_REQUIRED`, "This video may be inappropriate for some users." | video unavailable |
| Live stream or premiere | `videoDetails.isLiveContent: true` or `isLive`/`isUpcoming` | video unavailable |
| No English track | no `captionTracks`, or none with `languageCode` `en`/`en-*` | no English captions |
| Blocked by YouTube | reCAPTCHA page, or `LOGIN_REQUIRED` with "confirm you're not a bot" | internal error, logged and alerted; not the user's fault |

### Prototype results
The method was tested on 2026-11-12 with plain HTTP requests (curl), **from a local machine only**:

| Video | Type | Result |
|---|---|---|
| `dQw4w9WgXcQ` | Music video, manual + auto EN | ✅ manual `en`, 61 segments |
| `jNQXAC9IVRw` | "Me at the zoo", manual EN only | ✅ manual `en`, 6 segments |
| `arj7oStGLkU` | TED talk, manual + auto EN | ✅ manual `en`, 315 segments |
| `8jPQjjsBbIc` | TED talk, manual + auto EN | ✅ manual `en`, 260 segments |
| `kJQP7kiw5Fk` | Music video, 3 EN tracks | ✅ first `en`, 90 segments (needs the pick rule) |
| `9bZkp7q19f0` | Korean video, no EN track | ❌ rejected: no English captions |
| `HtVdAasjOgU` | Age-restricted | ❌ `LOGIN_REQUIRED` |
| `jfKfPfyJRdk` | Live stream | ❌ `UNPLAYABLE`, `isLiveContent: true` |
| `yYr8q0y5Jfg` | Unavailable | ❌ `UNPLAYABLE` |
| `aaaaaaaaaaa` | Invalid ID | ❌ `ERROR` |

Every rejection case can be told apart from the player response. **Not yet verified:** fetching from a Railway container through the forward proxy. This is tracked as a follow-up and must pass before the import feature ships.

## Consequences
- ✅ No third-party dependency for our biggest risk; small code that we fully understand and test.
- ✅ Can be swapped behind `ICaptionSource` if maintaining it becomes too expensive.
- ⚠️ **Fragile.** The method relies on YouTube internals (the `ANDROID` client version, response shape, XML format). Keep the client version in configuration, log parse failures with their own error code, and alert when the import failure rate jumps. When it breaks, compare with Python `youtube-transcript-api` and yt-dlp to see how they fixed it.
- ⚠️ **Proxy cost and dependency.** Imports fail if the proxy is down; proxy traffic costs money. Reusing stored captions for videos already imported (SPEC-002) keeps traffic low.
- ⚠️ **Auto-caption quality.** Little punctuation, uneven cue lengths, and tags like `[Music]` or `♪`. Segmentation has to handle these.
- Follow-up issues: implement the thin client (with unit tests on recorded responses), configure the forward proxy, add the rejection cases to the import flow, verify from Railway through the proxy, and monitor caption fetch failures.

## Legal and terms of service
We are not lawyers. These are risks to keep in mind, and they need a proper legal review before any commercial launch.

1. **YouTube's Terms of Service forbid automated access** to the service except through official APIs or with permission. This method, like yt-dlp and every caption library, is a breach of the ToS. Using a proxy to avoid blocking is circumvention, which makes it worse, not grey.
2. **YouTube can block us at any time**, technically (IP/bot detection) or legally (a demand letter), and owes us no notice or support. The product must survive caption import being down (existing lessons keep working).
3. **Copyright.** Manual captions belong to whoever wrote them (usually the channel); auto captions are derived from the owner's content. Storing and showing them is reproduction without a licence. The educational, transformative use and the short segments help, but they don't make it safe.
4. **Keep as little as possible.** Store the video ID, metadata and the caption cues a lesson needs. Never download, store or re-host audio or video.
5. **Playback through the official embedded player only** (IFrame API, SPEC-003), following the YouTube API Services Terms and branding guidelines: don't hide or cover the player, don't block ads, keep YouTube attribution.
6. **Takedowns.** We need a way to remove a video's captions and lessons quickly when a rights holder asks (DMCA or similar), plus a contact address for those requests.
7. **Proxy provider.** Check its own ToS (some forbid scraping targets like YouTube) and how it sources residential IPs; some networks use end users' devices without proper consent.
8. **Commercial use raises the risk.** A free personal or educational tool is lower risk; charging money for access to captions we fetch this way needs a legal review first.
9. **Privacy.** The URLs users submit and their lesson history are personal data, tied to their account. They need to be covered in the privacy policy, with deletion when an account is deleted.
