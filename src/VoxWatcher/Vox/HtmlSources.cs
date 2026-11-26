using System.Net;

namespace VoxWatcher.Vox;

public interface IHtmlSource
{
    Task<string> GetAsync(Uri url, CancellationToken ct);
}

/// <summary>Keeps a polite gap between requests to VOX (about 1.2 s plus jitter).</summary>
public sealed class RequestPacer
{
    private static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(1200);
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken ct)
    {
        var wait = _lastRequestUtc + Gap - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 600)), ct);
        _lastRequestUtc = DateTime.UtcNow;
    }
}

/// <summary>Real HTTP with polite pacing and retries.</summary>
public sealed class HttpHtmlSource(RequestPacer pacer) : IHtmlSource
{
    // One shared client for the whole process (static readonly, no lazy init).
    private static readonly HttpClient Client = CreateClient();

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10)];

    public int RequestCount { get; private set; }

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await GetOnceAsync(url, timeout: null, ct);
            }
            catch (TransientFetchException ex)
            {
                if (attempt >= RetryDelays.Length) throw new VoxSiteException(ex.Message);
            }

            await Task.Delay(RetryDelays[attempt], ct);
        }
    }

    /// <summary>One attempt, no retries. Used by auto mode to probe HTTP quickly.</summary>
    public async Task<string> GetOnceAsync(Uri url, TimeSpan? timeout, CancellationToken ct)
    {
        await pacer.WaitAsync(ct);
        RequestCount++;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) cts.CancelAfter(t);

        try
        {
            using var response = await Client.GetAsync(url, cts.Token);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(cts.Token);

            var message = $"VOX answered {(int)response.StatusCode} {response.ReasonPhrase} for {url.PathAndQuery}";
            var transient = response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;
            throw transient ? new TransientFetchException(message) : new VoxSiteException(message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException && !ct.IsCancellationRequested)
        {
            throw new TransientFetchException($"Could not reach VOX over HTTP ({ex.GetType().Name}: {ex.Message})");
        }
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

/// <summary>
/// VOX_FETCHER=auto. The first request goes over plain HTTP with a short timeout.
/// If that fails (timeout, reset, block), the rest of the process uses the real browser.
/// </summary>
public sealed class AutoHtmlSource(HttpHtmlSource http, Func<PlaywrightHtmlSource> browserFactory) : IHtmlSource, IAsyncDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private IHtmlSource? _chosen;
    private PlaywrightHtmlSource? _browser;

    public int RequestCount => http.RequestCount + (_browser?.RequestCount ?? 0);

    public string Chosen => _chosen switch
    {
        null => "undecided",
        PlaywrightHtmlSource => "browser",
        _ => "http"
    };

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        if (_chosen is not null) return await _chosen.GetAsync(url, ct);

        try
        {
            var html = await http.GetOnceAsync(url, ProbeTimeout, ct);
            _chosen = http;
            Console.WriteLine("Fetcher: auto -> http (first request succeeded)");
            return html;
        }
        catch (Exception ex) when (ex is TransientFetchException or VoxSiteException && !ct.IsCancellationRequested)
        {
            Console.WriteLine($"Fetcher: auto -> browser (HTTP failed: {ex.Message})");
            _browser = browserFactory();
            _chosen = _browser;
            return await _browser.GetAsync(url, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
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

/// <summary>A failure worth retrying (timeout, reset, 429, 5xx).</summary>
public sealed class TransientFetchException(string message) : Exception(message);
