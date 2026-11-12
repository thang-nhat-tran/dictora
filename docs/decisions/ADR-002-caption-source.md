# ADR-002: CC BY videos only, captions via YoutubeExplode behind an interface

- **Status:** Accepted
- **Date:** 2026-11-12
- **Issue:** #2
- **Related:** SPEC-002, ADR-001

## Context
YouTube captions are Dictora's only text source (SPEC-002), so how we fetch them is the biggest technical risk. What matters:
- **We only need subtitles**: English caption text with start time and duration. We don't need video streams, audio, or metadata beyond what the import already stores.
- **We run on Railway.** YouTube often blocks or rate-limits datacenter IPs ("Sign in to confirm you're not a bot"), so it isn't enough for the approach to work from a laptop.
- **No official API gives us captions.** The YouTube Data API v3 `captions.download` call requires OAuth as the *video owner*, and our users import other people's videos.
- **Legal risk.** Captions belong to the video's rights holder, and YouTube's terms limit automated access (see *Legal and terms of service*). To keep copyright risk low at this stage, **we only allow dictation on videos licensed CC BY**. On YouTube the only Creative Commons option is CC BY 3.0, so "Creative Commons" on YouTube means CC BY.
- **YouTube changes its internals without notice.** In 2025 YouTube started returning an empty body for caption URLs taken from the watch page. That change broke the older libraries.

## Options
1. **YouTube Data API v3 (`captions`)**
   - Pros: official and stable, within YouTube's terms.
   - Cons: [`captions.download`](https://developers.google.com/youtube/v3/docs/captions/download) "requires the user to have permission to edit the video", through OAuth (`youtube.force-ssl` or `youtubepartner` scope); anyone else gets `403 forbidden`. [`captions.list`](https://developers.google.com/youtube/v3/docs/captions/list) needs OAuth too. So it's **unusable** for importing other people's videos.
   - **CC BY doesn't change this.** The licence is the owner's permission to reuse the work; it doesn't give us API access to captions of a video we can't edit. The docs make no exception for public or Creative Commons videos.
   - Quota, for reference: `captions.list` costs 50 units and `captions.download` 200 units per call.
   - But `videos.list` needs only an API key and returns the licence, so we **use it for the licence check and metadata** (see Decision), not for captions.
2. **YoutubeExplode** (NuGet, actively maintained)
   - Pros: mature and popular, has a closed-captions client, and gets updated when YouTube changes.
   - Cons: a full video library (streams, playlists, channels, search, muxing). We'd use one small part of it and inherit the rest. We depend on its maintainer to keep up with YouTube.
3. **yt-dlp as a sidecar** (call the `yt-dlp` binary)
   - Pros: the most robust and fastest-updated extractor; handles many edge cases.
   - Cons: adds Python and a binary to our container, starts a process per request, and needs frequent updates. Far more than we need for subtitles.
4. **Our own thin client** (about 150 lines of C#), using the same method as the Python `youtube-transcript-api` library (v1.x)
   - How: read `INNERTUBE_API_KEY` from the watch page, `POST /youtubei/v1/player` with the `ANDROID` client context, then download the chosen caption track's `baseUrl` (without `&fmt=srv3`) and parse the `<text start dur>` XML.
   - Pros: only what we need; no third-party dependency; fits our module structure, `HttpClient` setup, logging and tests; easy to route through our proxy; we understand every line.
   - Cons: we maintain it. When YouTube changes something, we have to notice and fix it ourselves; there's no upstream release to wait for.

### Comparison

| | Works today | Only subtitles | Dependency risk | Proxy support | Effort to adopt |
|---|---|---|---|---|---|
| 1. Data API v3 (captions) | ❌ (owner OAuth) | ✅ | ✅ official | n/a | n/a |
| 2. **YoutubeExplode** | ✅ | ❌ whole video library | ✅ maintained | via `HttpClient` | Low |
| 3. yt-dlp sidecar | ✅ | ❌ | ⚠️ needs updates | ✅ | High (container, process) |
| 4. Own thin client | ✅ | ✅ | ✅ none (we own it) | ✅ | Medium |

## Decision
Importing a video is two steps:
1. **Licence and metadata check** with the official **YouTube Data API v3** (`videos.list`). Only CC BY, embeddable, public or unlisted videos that aren't live or age-restricted continue. **No captions are fetched for any other video.**
2. **Captions** with YoutubeExplode, behind our own interface.

### Step 1: licence check (CC BY only)
`GET https://www.googleapis.com/youtube/v3/videos?part=snippet,status,contentDetails&id={id}&key={API_KEY}`, behind its own interface (e.g. `IVideoInfoSource`, adapter `YouTubeDataApiVideoInfoSource`):
- **Licence:** `status.license` must be `creativeCommon` (CC BY 3.0). `youtube` (standard licence) is rejected.
- **The same response gives us** title, channel name and ID, duration, thumbnail, and the exact reason for most rejections (table below), so we don't scrape any of this.
- **Cost:** 1 quota unit per call; the default quota is 10,000 units/day, and one call can check up to 50 IDs. The API key is a Railway secret. This call is official, so it doesn't go through the proxy.
- **Stored with the video** for attribution and as evidence: licence `CC BY 3.0`, title, channel name and ID, URL, and the date of the check. A CC BY licence can't be withdrawn for copies made while it applied, so we keep the captions even if the owner later switches to the standard licence.

### Step 2: captions
We start with **YoutubeExplode behind our own interface**, `ICaptionSource`, in the Lessons module. Nothing outside the adapter knows which library fetches captions, so we can switch to our own thin client (option 4) or yt-dlp (option 3) by writing a new adapter, without changing the import flow.

Why YoutubeExplode first: it works today (same results as the raw method on all 10 sample videos), it's maintained and widely used, so fixes for YouTube changes come from upstream instead of from us, and it takes an `HttpClient`, which lets us route it through our proxy. Its extra features cost us nothing as long as we only use them inside the adapter.

### Caption interface
A sketch; the exact shape is decided in the implementation issue:

```csharp
public interface ICaptionSource
{
    Task<CaptionFetchResult> FetchEnglishCaptionsAsync(string videoId, CancellationToken ct);
}

// Success: language code, IsAutoGenerated, and cues (text, start, duration). Video info comes from step 1.
// Failure: VideoUnavailable | NoEnglishCaptions | SourceBlocked | SourceError.
// Called only after the licence check passed (step 1).
```

Rules for the adapter:
- **No YoutubeExplode types leak out.** The adapter (`YoutubeExplodeCaptionSource`) is `internal` and maps everything to our own types and failure reasons. It's the only file that references the package.
- **Version pinned** in `Directory.Packages.props` (`YoutubeExplode` 6.6.3), and updated on purpose when YouTube breaks something (see [lesson 03](../learning/03-package-and-tool-versions.md)).

### Rules
- **Network:** `YoutubeClient` gets a typed `HttpClient` from `IHttpClientFactory` that sends **every request through a forward proxy** set in configuration (URL and credentials as Railway secrets), to avoid datacenter-IP blocking. Locally the proxy is optional.
- **Choosing a track:** a manual English track (`en`, `en-*`) first, otherwise an auto-generated English track (`IsAutoGenerated`). If several match (Despacito has `en`, `en`, `en-US`), take the first `en`, then the first `en-*`.
- **Auto-generated captions are accepted** for now, despite weak punctuation and segmentation. SPEC-002 already falls back to splitting on pauses or length. We'll improve this later.
- **Rejected videos** (no dictation possible), mapped to the SPEC-002 error codes. Steps 1 and 2 run in this order and stop at the first failure:

| Case | How we detect it | Step | SPEC-002 error |
|---|---|---|---|
| Invalid video ID | the URL doesn't parse to an 11-character video ID | before 1 | invalid URL |
| Unknown, deleted or private | `videos.list` returns no item (private videos are hidden from API-key calls) | 1 | video unavailable |
| Age-restricted | `contentDetails.contentRating.ytRating` is `ytAgeRestricted` | 1 | video unavailable |
| Live or upcoming | `snippet.liveBroadcastContent` is `live` or `upcoming` | 1 | video unavailable |
| Not embeddable | `status.embeddable` is `false` (practice needs the embedded player, SPEC-003) | 1 | video unavailable |
| Standard YouTube licence | `status.license` is not `creativeCommon` | 1 | not CC BY licensed |
| No English track | no caption track whose language code is `en`/`en-*` | 2 | no English captions |
| Unplayable anyway | `VideoUnplayableException` from YoutubeExplode | 2 | video unavailable |
| Rate-limited by YouTube | `RequestLimitExceededException` | 2 | internal error, logged and alerted |
| Data API quota or key error | HTTP 403 `quotaExceeded` / 400 `keyInvalid` | 1 | internal error, logged and alerted |
| Any other error | `HttpRequestException`, YoutubeExplode parse errors | 1 or 2 | internal error, logged and alerted |

### Prototype results
Tested on 2026-11-12 **from a local machine only**, with YoutubeExplode 6.6.3 and, to cross-check, with the raw innertube method (`ANDROID` player API, as used by Python `youtube-transcript-api` v1.x) via curl. Both gave the same tracks and segments:

| Video | Type | YoutubeExplode | Raw method |
|---|---|---|---|
| `dQw4w9WgXcQ` | Music video, manual + auto EN | ✅ manual `en`, 61 segments | ✅ same |
| `jNQXAC9IVRw` | "Me at the zoo", manual EN only | ✅ manual `en`, 6 segments | ✅ same |
| `arj7oStGLkU` | TED talk, manual + auto EN | ✅ manual `en`, 315 segments | ✅ same |
| `8jPQjjsBbIc` | TED talk, manual + auto EN | ✅ manual `en`, 260 segments | ✅ same |
| `kJQP7kiw5Fk` | Music video, 3 EN tracks | ✅ first `en`, 90 segments (needs the pick rule) | ✅ same |
| `9bZkp7q19f0` | Korean video, no EN track | ❌ no `en` track | ❌ no `en` track |
| `HtVdAasjOgU` | Age-restricted | ❌ `VideoUnplayableException` | ❌ `LOGIN_REQUIRED` |
| `jfKfPfyJRdk` | Live stream | ❌ `VideoUnplayableException` | ❌ `UNPLAYABLE`, `isLiveContent` |
| `yYr8q0y5Jfg` | Unavailable | ❌ `VideoUnplayableException` | ❌ `UNPLAYABLE` |
| `aaaaaaaaaaa` | Invalid ID | ❌ `VideoUnavailableException` | ❌ `ERROR` |

**Licence check:** the Data API call wasn't tested in this spike (it needs an API key). On the watch page, Big Buck Bunny (`aqz-KE-bpKQ`, Blender Foundation) shows "Creative Commons Attribution license (reuse allowed)", while Sintel (`eRsGyueVLvQ`) and `dQw4w9WgXcQ` show nothing, i.e. the standard licence. **Under the CC BY rule, most or all of the 10 sample videos above would be rejected at step 1**: they were picked to test caption cases, not licences. A new set of CC BY sample videos (manual and auto captions) is needed for the unit tests.

**Not yet verified:** fetching from a Railway container through the forward proxy. This is tracked as a follow-up and must pass before the import feature ships.

## Consequences
- ✅ **Much lower copyright risk:** we only store captions of videos whose owners allow reuse, with the attribution CC BY requires.
- ✅ Exact rejection reasons from the Data API (age rating, live, embeddable), which YoutubeExplode alone doesn't give.
- ✅ Working captions now, with little code of our own; upstream fixes YouTube changes.
- ✅ Swappable: switching to our own client or yt-dlp means one new adapter behind `ICaptionSource`.
- ⚠️ **Much smaller catalogue.** Few YouTube videos are CC BY, and fewer have good English speech. Users must look for them (YouTube search filter: *Creative Commons*). This is a product risk to watch; a curated list of CC BY videos may be needed.
- ⚠️ **Data API rules apply.** We must follow the YouTube API Services Terms and Developer Policies. Those policies limit how long API data (title, channel name, licence) may be stored before it's refreshed (30 days at the time of writing), so a background job re-fetches it in batches of 50. Check the current policies before shipping.
- ⚠️ **Unplayable at step 2.** If a video passes step 1 but YoutubeExplode still says unplayable, it may be YouTube's bot check. Alert when that rate jumps.
- ⚠️ **Still fragile.** Upstream fixes take days to release. When imports break, update the package first; if no fix exists yet, the raw method above (option 4) is the fallback adapter.
- ⚠️ **A large dependency** for a small need. Only the adapter may use it; the architecture tests can check this.
- ⚠️ **Proxy cost and dependency.** Imports fail if the proxy is down; proxy traffic costs money. Reusing stored captions for videos already imported (SPEC-002) keeps traffic low.
- ⚠️ **Auto-caption quality.** Little punctuation, uneven cue lengths, and tags like `[Music]` or `♪`. Segmentation has to handle these.
- Follow-up issues: implement the licence check (`IVideoInfoSource` with the Data API, plus the 30-day refresh job), create the Google Cloud project and API key, collect CC BY sample videos, implement `ICaptionSource` with the YoutubeExplode adapter (with unit tests on recorded responses), configure the forward proxy, add the rejection cases to the import flow, verify from Railway through the proxy, and monitor caption fetch failures.

## Legal and terms of service
We are not lawyers. These are risks to keep in mind, and they need a proper legal review before any commercial launch. The CC BY rule addresses copyright (point 3) but **not** YouTube's terms (point 1).

1. **YouTube's Terms of Service forbid automated access** to the service except through official APIs or with permission. Fetching captions with YoutubeExplode (or yt-dlp, or our own client) is a breach of the ToS, **even for CC BY videos**: the licence is the owner's permission to reuse the work, not YouTube's permission to access the service this way. Only the licence check (Data API) is official; the official captions API can't be used for CC BY videos we don't own (option 1). Using a proxy to avoid blocking is circumvention, which makes it worse, not grey.
2. **YouTube can block us at any time**, technically (IP/bot detection) or legally (a demand letter), and owes us no notice or support. The product must survive caption import being down (existing lessons keep working).
3. **Copyright, reduced by CC BY.** CC BY allows copying, adapting and commercial use **with attribution**: title, creator (channel), link to the source, the licence with a link, and a note that we changed it (we segment the captions). SPEC-002 and SPEC-003 require showing this wherever captions appear. Remaining gaps:
   - The uploader may not own what they licensed (re-uploads, background music, clips from TV). We rely on their licence in good faith, record when we checked it, and remove content on request.
   - Manual captions are almost always the owner's (YouTube ended community captions in 2020). Auto-generated captions are a transcript of the CC BY work, so they fall under the same licence, but YouTube's own part in generating them isn't explicitly licensed.
4. **Keep as little as possible.** Store the video ID, metadata and the caption cues a lesson needs. Never download, store or re-host audio or video.
5. **YouTube API Services Terms and Developer Policies** apply because we call the Data API: limits on storing API data (refresh schedule), quota, and showing that data accurately. **Playback through the official embedded player only** (IFrame API, SPEC-003), following the YouTube API Services Terms and branding guidelines: don't hide or cover the player, don't block ads, keep YouTube attribution.
6. **Takedowns.** We need a way to remove a video's captions and lessons quickly when a rights holder asks (DMCA or similar), plus a contact address for those requests.
7. **Proxy provider.** Check its own ToS (some forbid scraping targets like YouTube) and how it sources residential IPs; some networks use end users' devices without proper consent.
8. **Commercial use.** CC BY allows commercial use, so the licence itself isn't a blocker. The ToS issue in point 1 remains, so charging money still needs a legal review first.
9. **Privacy.** The URLs users submit and their lesson history are personal data, tied to their account. They need to be covered in the privacy policy, with deletion when an account is deleted.
