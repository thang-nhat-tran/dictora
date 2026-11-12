using System.Text.Json;
using System.Web;

namespace Dictora.Modules.Lessons.Tests;

/// <summary>
/// Answers HTTP requests from a recording of real YouTube traffic in <c>Fixtures/YouTube/&lt;videoId&gt;.json</c>,
/// so the YoutubeExplode adapter runs end to end without the network.
/// A request matches a recorded one by method, path and the <c>v</c>/<c>lang</c>/<c>kind</c> query values;
/// repeated matches are answered in recorded order. An unknown request fails the test.
/// </summary>
internal sealed class ReplayHttpHandler : HttpMessageHandler
{
    private static readonly string[] MatchedQueryKeys = ["v", "lang", "kind"];

    private readonly List<RecordedExchange> _remaining;

    public ReplayHttpHandler(string videoId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "YouTube", videoId + ".json");
        _remaining = JsonSerializer.Deserialize<List<RecordedExchange>>(File.ReadAllText(path), JsonOptions)!;
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var key = Key(request.Method.Method, request.RequestUri!);
        var exchange = _remaining.FirstOrDefault(e => Key(e.Method, new Uri(e.Url)) == key)
            ?? throw new InvalidOperationException($"No recorded response for {key}. Re-record the fixture if YoutubeExplode changed its requests.");
        _remaining.Remove(exchange);

        var response = new HttpResponseMessage((System.Net.HttpStatusCode)exchange.Status)
        {
            Content = new StringContent(exchange.Body),
            RequestMessage = request,
        };
        if (exchange.ContentType is not null)
        {
            response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(exchange.ContentType);
        }

        return Task.FromResult(response);
    }

    private static string Key(string method, Uri uri)
    {
        var query = HttpUtility.ParseQueryString(uri.Query);
        var matched = MatchedQueryKeys.Select(k => $"{k}={query[k]}");
        return $"{method} {uri.AbsolutePath}?{string.Join('&', matched)}";
    }

    private sealed record RecordedExchange(string Method, string Url, int Status, string? ContentType, string Body);
}
