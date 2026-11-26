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

IHtmlSource source = fixtures is not null ? new FixtureHtmlSource(fixtures) : new HttpHtmlSource();
var runner = new WatchRunner(config, source, notifier, new StateStore(stateDir), tz, now);
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));

switch (mode)
{
    case "watch":
        await runner.RunAsync(cts.Token);
        if (source is HttpHtmlSource http) Console.WriteLine($"Done. {http.RequestCount} request(s) to VOX.");
        return 0;

    case "catalog":
        await runner.PrintCatalogAsync(cts.Token);
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
