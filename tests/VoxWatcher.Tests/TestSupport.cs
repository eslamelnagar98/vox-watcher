using System.Globalization;
using System.Text.RegularExpressions;
using VoxWatcher.Configuration;
using VoxWatcher.Engine;
using VoxWatcher.Notifications;
using VoxWatcher.State;
using VoxWatcher.Vox;

namespace VoxWatcher.Tests;

internal static class Fixtures
{
    /// <summary>Reads a file from Fixtures/ with \n line endings, whatever git checked out.</summary>
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath)).ReplaceLineEndings("\n");
}

/// <summary>
/// An in-memory egy.voxcinemas.com for Avengers: Doomsday at City Centre Almaza, built from the
/// captured HTML in Fixtures/Scenario. Pages are keyed like <see cref="FixtureHtmlSource"/> file names.
/// </summary>
internal sealed partial class FakeVoxSite(Dictionary<string, string> pages) : IHtmlSource
{
    private const string BaseQuery = "c_city-centre-almaza_m_avengers-doomsday";
    private const string NoShowtimes = "_no-showtimes.html";

    private static readonly string Catalog = Fixtures.Read("Scenario/catalog.html");
    private static readonly string Nav = Fixtures.Read("Scenario/nav.html");
    private static readonly string D18 = Fixtures.Read("Scenario/d18.html");
    private static readonly string D20 = Fixtures.Read("Scenario/d20.html");

    public static FakeVoxSite Create(bool gold715Bookable = false, bool add24Dec = false, int? brokenDate = null, bool brokenFirstPage = false)
    {
        var nav = Nav;
        if (add24Dec)
        {
            nav = nav.Replace("</ol>\n</div>",
                "<li>\n<a href=\"/showtimes?c&#x3D;city-centre-almaza&amp;m&#x3D;avengers-doomsday&amp;d&#x3D;20261224\">Thu 24 Dec</a>\n</li>\n</ol>\n</div>");
        }

        var d18 = D18;
        if (gold715Bookable)
        {
            d18 = d18.Replace(
                "<li data-id=\"0047-276906\">\n<span class=\"action showtime unavailable\">7:15pm</span>",
                "<li data-id=\"0047-276906\">\n<a class=\"action showtime\" href=\"https://egy.voxcinemas.com/booking/0047-276906\" data-id=\"0047-276906\">7:15pm </a>");
        }

        // Each date reuses a captured day with its session IDs shifted, so every session ID is unique.
        var days = new Dictionary<int, string>
        {
            [16] = ShiftIds(D18, 5000), [17] = ShiftIds(D18, 6000), [18] = d18, [19] = ShiftIds(D18, 7000),
            [20] = D20, [21] = ShiftIds(D20, 8000), [22] = ShiftIds(D20, 9000), [23] = ShiftIds(D20, 10000),
        };
        if (add24Dec) days[24] = ShiftIds(D18, 11000);

        string Page(string body) => Catalog + "\n" + nav + "\n" + body;
        const string accessDenied = "<html><body>Access Denied</body></html>";

        var result = new Dictionary<string, string>
        {
            ["_root.html"] = Catalog,
            [NoShowtimes] = Catalog + "<p>No showtimes could be found for your current cinema(s), movie(s) and date.</p>",
            // A glitchy first page: catalog and date tabs, but no showtimes and no "no showtimes" message.
            [$"{BaseQuery}.html"] = brokenFirstPage ? Catalog + "\n" + nav : Page(days[16]),
        };
        foreach (var (day, body) in days)
            result[$"{BaseQuery}_d_202612{day:00}.html"] = day == brokenDate ? accessDenied : Page(body);

        return new FakeVoxSite(result);
    }

    /// <summary>The whole site is down or blocked.</summary>
    public static FakeVoxSite Down()
    {
        const string html = "<html><body>Service unavailable</body></html>";
        return new FakeVoxSite(new() { ["_root.html"] = html, [NoShowtimes] = html });
    }

    public Task<string> GetAsync(Uri url, CancellationToken ct)
    {
        var name = FixtureHtmlSource.FileNameFor(url);
        if (pages.TryGetValue(name, out var html) || pages.TryGetValue(NoShowtimes, out html))
            return Task.FromResult(html);
        throw new VoxSiteException($"No fake page for {url.PathAndQuery}");
    }

    private static string ShiftIds(string html, int offset) =>
        SessionId().Replace(html, m => $"0047-{int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + offset}");

    [GeneratedRegex(@"0047-(\d+)")]
    private static partial Regex SessionId();
}

/// <summary>Runs the real <see cref="WatchRunner"/> step by step against a fake site, with its own state folder and config.</summary>
internal sealed class Scenario : IDisposable
{
    public const string WatchKey = "doomsday-at-almaza";

    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    private readonly string _stateDir = Directory.CreateTempSubdirectory("vox-watcher-tests-").FullName;
    private readonly ConsoleNotifier _notifier = new();

    /// <summary>Test-only config. Never the repo's watches.json, which the user edits.</summary>
    public WatchConfig Config { get; } = new()
    {
        Site = "https://egy.voxcinemas.com",
        TimeZone = "Africa/Cairo",
        Watches =
        [
            new WatchDefinition
            {
                Name = "Doomsday at Almaza",
                Cinemas = ["city-centre-almaza"],
                Movies = ["avengers-doomsday"],
                Dates = new DateFilter { From = new DateOnly(2026, 12, 16), To = new DateOnly(2027, 1, 15) },
                Time = new TimeFilter { After = "12:00", Before = "23:59" },
            },
        ],
    };

    private StateStore Store => new(_stateDir);

    public WatcherState State => Store.Load();

    public List<string> SessionIds => [.. State.Watches[WatchKey].Sessions.Keys];

    public string[] Events => File.Exists(Store.EventsLogPath) ? File.ReadAllLines(Store.EventsLogPath) : [];

    public string ReadStateFile(string name) => File.ReadAllText(Path.Combine(_stateDir, name));

    /// <summary>One check at the given UTC time. Returns the notifications it sent.</summary>
    public async Task<List<Notification>> RunAsync(IHtmlSource site, string utc)
    {
        // Messages format dates with the current culture, as in production (Program.cs sets invariant).
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var before = _notifier.Sent.Count;
        var now = DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture);
        await new WatchRunner(Config, site, _notifier, Store, Cairo, now).RunAsync(TestContext.Current.CancellationToken);
        return [.. _notifier.Sent.Skip(before)];
    }

    public void Dispose() => Directory.Delete(_stateDir, recursive: true);
}
