using System.Net;

namespace VoxWatcher.Vox;

public interface IHtmlSource
{
    Task<string> GetAsync(Uri url, CancellationToken ct);
}

/// <summary>Real HTTP with polite pacing and retries.</summary>
public sealed class HttpHtmlSource : IHtmlSource
{
    // One shared client for the whole process (static readonly, no lazy init).
    private static readonly HttpClient Client = CreateClient();

    private static readonly TimeSpan PauseBetweenRequests = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10)];

    private DateTime _lastRequestUtc = DateTime.MinValue;

    public int RequestCount { get; private set; }

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            await PaceAsync(ct);
            RequestCount++;

            try
            {
                using var response = await Client.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(ct);

                var transient = response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
                if (!transient || attempt >= RetryDelays.Length)
                    throw new VoxSiteException($"VOX answered {(int)response.StatusCode} {response.ReasonPhrase} for {url.PathAndQuery}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= RetryDelays.Length)
                    throw new VoxSiteException($"Could not reach VOX ({ex.GetType().Name}: {ex.Message})");
            }

            await Task.Delay(RetryDelays[attempt], ct);
        }
    }

    private async Task PaceAsync(CancellationToken ct)
    {
        var wait = _lastRequestUtc + PauseBetweenRequests - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 600)), ct);
        _lastRequestUtc = DateTime.UtcNow;
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }
}

/// <summary>Offline source for testing: maps each URL query to a saved HTML file.</summary>
public sealed class FixtureHtmlSource(string directory) : IHtmlSource
{
    public Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        var path = Path.Combine(directory, FileNameFor(url));
        if (File.Exists(path)) return File.ReadAllTextAsync(path, ct);

        var fallback = Path.Combine(directory, "_no-showtimes.html");
        if (File.Exists(fallback)) return File.ReadAllTextAsync(fallback, ct);

        throw new VoxSiteException($"No fixture for {url.PathAndQuery} (expected {path})");
    }

    public static string FileNameFor(Uri url)
    {
        var q = url.Query.TrimStart('?');
        var safe = new string(q.Select(c => char.IsLetterOrDigit(c) || c is '-' ? c : '_').ToArray());
        return (string.IsNullOrEmpty(safe) ? "_root" : safe) + ".html";
    }
}

public sealed class VoxSiteException(string message) : Exception(message);
