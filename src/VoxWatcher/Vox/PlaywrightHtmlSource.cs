using Microsoft.Playwright;

namespace VoxWatcher.Vox;

/// <summary>
/// Fetches pages with a real headless Chromium. VOX sits behind Akamai, which resets
/// plain HttpClient connections from some networks but serves a normal browser.
/// One browser and one context per process, reused for every request.
/// Returns the raw server HTML (response.TextAsync), which is what the parser expects.
/// </summary>
/// <remarks>
/// Probe on a GitHub ubuntu-24.04 runner (AWS): Playwright's bundled Chromium headless shell gets
/// ERR_HTTP2_PROTOCOL_ERROR from VOX, while the installed Google Chrome (channel "chrome") gets 200.
/// So the default channel is "chrome". Override with VOX_BROWSER_CHANNEL (chrome, msedge, or chromium).
/// </remarks>
public sealed class PlaywrightHtmlSource(RequestPacer pacer, string timeZone, string channel) : IHtmlSource, IAsyncDisposable
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10)];
    private const float NavigationTimeoutMs = 45_000;

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public int RequestCount { get; private set; }

    public async Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        var page = await EnsureStartedAsync(ct);

        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await pacer.WaitAsync(ct);
            RequestCount++;

            string failure;
            try
            {
                var response = await page.GotoAsync(url.ToString(), new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = NavigationTimeoutMs
                });

                if (response is null)
                {
                    failure = $"Browser got no response for {url.PathAndQuery}";
                }
                else if (response.Ok)
                {
                    return await response.TextAsync();
                }
                else
                {
                    failure = $"VOX answered {response.Status} {response.StatusText} for {url.PathAndQuery} (browser)";
                    var transient = response.Status is 429 or >= 500;
                    if (!transient) throw new VoxSiteException(failure);
                }
            }
            catch (PlaywrightException ex)
            {
                failure = $"Browser could not load {url.PathAndQuery} ({ex.GetType().Name}: {FirstLine(ex.Message)})";
            }

            if (attempt >= RetryDelays.Length) throw new VoxSiteException(failure);
            await Task.Delay(RetryDelays[attempt], ct);
        }
    }

    private async Task<IPage> EnsureStartedAsync(CancellationToken ct)
    {
        if (_page is not null) return _page;

        await _startLock.WaitAsync(ct);
        try
        {
            if (_page is not null) return _page;

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = channel is "" or "chromium" ? null : channel,
                Headless = true,
                Args = ["--disable-blink-features=AutomationControlled"]
            });

            // Default headless UA contains "HeadlessChrome", which Akamai rejects. Use a normal Chrome UA.
            var major = _browser.Version.Split('.')[0];
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = $"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{major}.0.0.0 Safari/537.36",
                Locale = "en-US",
                TimezoneId = timeZone
            });

            await _context.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType is "image" or "media" or "font")
                    await route.AbortAsync();
                else
                    await route.ContinueAsync();
            });

            _page = await _context.NewPageAsync();
            Console.WriteLine($"Browser started: {(channel is "" or "chromium" ? "chromium" : channel)} {_browser.Version}");
            return _page;
        }
        catch (PlaywrightException ex)
        {
            throw new VoxSiteException($"Could not start the browser ({FirstLine(ex.Message)}). Is the \"{channel}\" browser installed?");
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
        _startLock.Dispose();
    }

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();
}
