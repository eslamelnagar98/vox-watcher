using System.Globalization;
using VoxWatcher.Configuration;
using VoxWatcher.Engine;
using VoxWatcher.Notifications;
using VoxWatcher.State;
using VoxWatcher.Vox;

// Usage (from the repo root):
//   dotnet run --project src/VoxWatcher -- watch          check VOX and send alerts (default)
//   dotnet run --project src/VoxWatcher -- catalog        print all cinema and movie slugs
//   dotnet run --project src/VoxWatcher -- test-notify    send a test push to your phone
// Options:
//   --dry-run              print notifications instead of sending them
//   --config <path>        default: watches.json
//   --state <dir>          default: state
//   --fixtures <dir>       read saved HTML files instead of the live site (testing)
//   --now <ISO time>       pretend it is this time (testing)
//   --loop-seconds <n>     repeat the check for about n seconds in one process (default 0 = once)
//   --interval-seconds <n> gap between passes, plus 0 to 15 s jitter (default 70)
// Env:
//   VOX_FETCHER=http|browser|auto  how to load VOX pages (default auto: HTTP first, browser on failure)
//   VOX_BROWSER_CHANNEL=chrome     browser for http fallback / browser mode (chrome, msedge, chromium)

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var mode = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "watch";
string? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var dryRun = args.Contains("--dry-run");
var configPath = Option("--config") ?? "watches.json";
var stateDir = Option("--state") ?? "state";
var fixtures = Option("--fixtures");
var now = Option("--now") is { } n ? DateTimeOffset.Parse(n, CultureInfo.InvariantCulture) : DateTimeOffset.UtcNow;

WatchConfig config;
try
{
    config = WatchConfig.Load(configPath);
}
catch (ConfigException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2; // Config errors should fail loudly so you see a red run in GitHub Actions.
}

TimeZoneInfo tz;
try { tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone); }
catch (TimeZoneNotFoundException) { tz = TimeZoneInfo.Utc; Console.WriteLine($"Time zone {config.TimeZone} not found. Using UTC."); }

var topic = Environment.GetEnvironmentVariable("NTFY_TOPIC");

// In GitHub Actions a missing secret must fail loudly. Otherwise the first run would
// record a silent baseline and you would never get the "Watching" confirmation.
if (!dryRun && string.IsNullOrWhiteSpace(topic) && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
{
    Console.Error.WriteLine("NTFY_TOPIC secret is missing. Add it in Settings > Secrets and variables > Actions.");
    return 2;
}

INotifier notifier = dryRun || string.IsNullOrWhiteSpace(topic)
    ? new ConsoleNotifier()
    : new NtfyNotifier(
        Environment.GetEnvironmentVariable("NTFY_SERVER") is { Length: > 0 } s ? s : "https://ntfy.sh",
        topic,
        Environment.GetEnvironmentVariable("NTFY_TOKEN"));

if (notifier is ConsoleNotifier && !dryRun)
    Console.WriteLine("NTFY_TOPIC is not set. Notifications are printed only.");

var pacer = new RequestPacer();
var fetcherMode = (Environment.GetEnvironmentVariable("VOX_FETCHER") ?? "auto").Trim().ToLowerInvariant();
var browserChannel = (Environment.GetEnvironmentVariable("VOX_BROWSER_CHANNEL") ?? "chrome").Trim().ToLowerInvariant();

// --fixtures always wins, so tests never touch the network.
IHtmlSource source = fixtures is not null
    ? new FixtureHtmlSource(fixtures)
    : fetcherMode switch
    {
        "http" => new HttpHtmlSource(pacer),
        "browser" => new PlaywrightHtmlSource(pacer, config.TimeZone, browserChannel),
        "auto" => new AutoHtmlSource(new HttpHtmlSource(pacer), () => new PlaywrightHtmlSource(pacer, config.TimeZone, browserChannel)),
        _ => null!
    };
if (source is null)
{
    Console.Error.WriteLine($"VOX_FETCHER must be http, browser or auto (got \"{fetcherMode}\").");
    return 2;
}
Console.WriteLine($"Fetcher: {(fixtures is not null ? "fixtures" : fetcherMode)}");

var loopSeconds = int.TryParse(Option("--loop-seconds"), out var ls) ? Math.Max(0, ls) : 0;
var intervalSeconds = int.TryParse(Option("--interval-seconds"), out var iv) ? Math.Max(10, iv) : 70;

// Must stay below the job's timeout-minutes in the workflow.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(240, loopSeconds + 180)));
var store = new StateStore(stateDir);

try
{
    switch (mode)
    {
        case "watch":
            await RunWatchLoopAsync();
            Console.WriteLine($"Done. {RequestCount(source)} request(s) to VOX. Fetcher used: {FetcherUsed(source)}.");
            return 0;

        case "catalog":
            await new WatchRunner(config, source, notifier, store, tz, now).PrintCatalogAsync(cts.Token);
            return 0;

        case "test-notify":
            await notifier.SendAsync(new Notification(
                "VOX watcher test",
                "If you can read this on your phone, alerts work. Booking alerts arrive with the highest priority.",
                5, ["white_check_mark"], config.Site + "/showtimes"), cts.Token);
            return 0;

        default:
            Console.Error.WriteLine($"Unknown mode \"{mode}\". Use watch, catalog or test-notify.");
            return 2;
    }
}
finally
{
    if (source is IAsyncDisposable disposable) await disposable.DisposeAsync();
}

// Repeats the whole check inside one job (two checks per run with the workflow's settings), sharing one browser.
// Each pass is a new WatchRunner with a fresh "now". The browser (if any) is shared across passes.
async Task RunWatchLoopAsync()
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var fixedNow = Option("--now") is not null;

    for (var pass = 1; ; pass++)
    {
        var passNow = fixedNow ? now : DateTimeOffset.UtcNow;
        if (loopSeconds > 0) Console.WriteLine($"--- pass {pass} at {TimeZoneInfo.ConvertTime(passNow, tz):HH:mm:ss} ---");

        await new WatchRunner(config, source, notifier, store, tz, passNow).RunAsync(cts.Token);

        var gap = TimeSpan.FromSeconds(intervalSeconds + Random.Shared.Next(0, 15));
        if (loopSeconds == 0 || clock.Elapsed + gap > TimeSpan.FromSeconds(loopSeconds)) break;
        await Task.Delay(gap, cts.Token);
    }
}

static int RequestCount(IHtmlSource source) => source switch
{
    HttpHtmlSource h => h.RequestCount,
    PlaywrightHtmlSource b => b.RequestCount,
    AutoHtmlSource a => a.RequestCount,
    _ => 0
};

static string FetcherUsed(IHtmlSource source) => source switch
{
    HttpHtmlSource => "http",
    PlaywrightHtmlSource => "browser",
    AutoHtmlSource a => "auto -> " + a.Chosen,
    _ => "fixtures"
};
