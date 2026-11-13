using Dictora.Modules.Lessons.Infrastructure.YouTube;
using YoutubeExplode.Exceptions;

namespace Dictora.Modules.Lessons.Tests;

public class YoutubeExplodeCaptionSourceTests
{
    [Fact]
    public void Rate_limiting_maps_to_blocked() =>
        Assert.IsType<CaptionFetchResult.Blocked>(
            YoutubeExplodeCaptionSource.MapFailure(new RequestLimitExceededException("429")));

    [Fact]
    public void Unplayable_video_maps_to_video_unavailable() =>
        Assert.IsType<CaptionFetchResult.VideoUnavailable>(
            YoutubeExplodeCaptionSource.MapFailure(new VideoUnplayableException("LOGIN_REQUIRED")));

    [Fact]
    public void Unavailable_video_maps_to_video_unavailable() =>
        Assert.IsType<CaptionFetchResult.VideoUnavailable>(
            YoutubeExplodeCaptionSource.MapFailure(new VideoUnavailableException("ERROR")));

    [Fact]
    public void Other_errors_map_to_failed_with_the_reason()
    {
        var result = YoutubeExplodeCaptionSource.MapFailure(new HttpRequestException("connection reset"));

        var failed = Assert.IsType<CaptionFetchResult.Failed>(result);
        Assert.Contains("connection reset", failed.Reason);
    }

    [Fact]
    public async Task Network_errors_are_returned_as_failed_not_thrown()
    {
        using var http = new HttpClient(new ThrowingHandler());
        var source = new YoutubeExplodeCaptionSource(http);

        var result = await source.FetchEnglishCaptionsAsync("jNQXAC9IVRw", CancellationToken.None);

        Assert.IsType<CaptionFetchResult.Failed>(result);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("no network in tests");
    }
}
