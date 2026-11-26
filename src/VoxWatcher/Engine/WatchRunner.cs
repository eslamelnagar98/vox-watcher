using System.Globalization;
using System.Text.Json;
using VoxWatcher.Configuration;
using VoxWatcher.Domain;
using VoxWatcher.Notifications;
using VoxWatcher.State;
using VoxWatcher.Vox;

namespace VoxWatcher.Engine;

public sealed class WatchRunner(
    WatchConfig config,
    IHtmlSource source,
    INotifier notifier,
    StateStore store,
    TimeZoneInfo tz,
    DateTimeOffset now)
{
    private const int HealthAlertAfterFailures = 3;
    private static readonly TimeSpan HealthAlertAfter = TimeSpan.FromMinutes(15);

    private readonly DateTime _nowLocal = TimeZoneInfo.ConvertTime(now, tz).DateTime;
    private DateOnly Today => DateOnly.FromDateTime(_nowLocal);
    private List<CatalogItem> _cinemas = [];
    private List<CatalogItem> _movies = [];

    public async Task RunAsync(CancellationToken ct)
    {
        var state = store.Load();
        var log = new List<string>();

        try
        {
            var watches = config.Watches.Where(w => w.Enabled).ToList();
            if (watches.Count == 0) Console.WriteLine("No enabled watches in watches.json. Only the catalog is refreshed.");

            // Catalog first: it also tells us if the site is reachable and still looks the same.
            await RefreshCatalogAsync(state, watches, log, ct);

            foreach (var watch in watches)
                await RunWatchAsync(watch, state, log, ct);

            await RecoverHealthAsync(state, ct);
        }
        catch (VoxSiteException ex)
        {
            await RecordFailureAsync(state, ex.Message, ct);
        }

        store.AppendEvents(log);
        store.Save(state);
        store.WriteText("INSIGHTS.md", InsightsWriter.Build(state, config, tz, Today));
    }

    public async Task PrintCatalogAsync(CancellationToken ct)
    {
        var page = await FetchAsync(VoxUrls.Showtimes(config.Site, [], [], null), ct);
        Console.WriteLine("CINEMAS (use in \"cinemas\")");
        foreach (var c in page.Cinemas.OrderBy(c => c.Name)) Console.WriteLine($"  {c.Slug,-45} {c.Name}");
        Console.WriteLine();
        Console.WriteLine("MOVIES (use in \"movies\")");
        foreach (var m in page.Movies.OrderBy(m => m.Name)) Console.WriteLine($"  {m.Slug,-45} {m.Name}");
        WriteCatalog(page.Cinemas, page.Movies);
    }

    /// <summary>CATALOG.md for people, catalog.json for the web app's cinema and movie pickers.</summary>
    private void WriteCatalog(IReadOnlyList<CatalogItem> cinemas, IReadOnlyList<CatalogItem> movies)
    {
        store.WriteText("CATALOG.md", InsightsWriter.BuildCatalog(cinemas, movies, config.Site));
        var catalog = new
        {
            config.Site,
            Cinemas = cinemas.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            Movies = movies.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
        };
        store.WriteText("catalog.json", JsonSerializer.Serialize(catalog, WatchConfig.JsonOptions));
    }

    private async Task RefreshCatalogAsync(WatcherState state, List<WatchDefinition> watches, List<string> log, CancellationToken ct)
    {
        var page = await FetchAsync(VoxUrls.Showtimes(config.Site, [], [], null), ct);
        if (page.Cinemas.Count == 0 && page.Movies.Count == 0)
            throw new VoxSiteException("The showtimes page has no cinema or movie list. VOX may have changed the page layout.");

        _cinemas = [.. page.Cinemas];
        _movies = [.. page.Movies];
        WriteCatalog(_cinemas, _movies);

        var firstTime = state.KnownMovies.Count == 0;
        var newMovies = _movies.Where(m => !state.KnownMovies.Contains(m.Slug)).ToList();
        foreach (var m in newMovies) state.KnownMovies.Add(m.Slug);
        if (firstTime || newMovies.Count == 0) return;

        foreach (var m in newMovies)
        {
            log.Add(Line("MovieListed", "-", m.Name, null, "", "", null, m.Slug));
            var watched = watches.Any(w => w.Movies.Contains(m.Slug, StringComparer.OrdinalIgnoreCase));
            if (watched || config.AnnounceNewMovies)
            {
                await notifier.SendAsync(new Notification(
                    $"New on VOX: {m.Name}",
                    watched ? "A movie you are watching is now on the VOX site. Showtimes alerts are active." : "A new movie was added to the VOX site.",
                    watched ? 4 : 2, ["clapper"], VoxUrls.Showtimes(config.Site, [], [m.Slug], null).ToString()), ct);
            }
        }
    }

    private async Task RunWatchAsync(WatchDefinition watch, WatcherState state, List<string> log, CancellationToken ct)
    {
        var warnings = Validate(watch);
        foreach (var w in warnings) Console.WriteLine($"  WARNING [{watch.Name}] {w}");

        var baseUrl = VoxUrls.Showtimes(config.Site, watch.Cinemas, watch.Movies, null);
        var first = await FetchAsync(baseUrl, ct);
        EnsureTrustworthy(first, requested: null, watch);

        var datesInScope = first.Dates.Where(d => d >= Today && watch.MatchesDate(d)).ToList();
        var showtimes = new List<Showtime>();

        foreach (var date in datesInScope)
        {
            var page = date == first.PageDate ? first : await FetchAsync(VoxUrls.Showtimes(config.Site, watch.Cinemas, watch.Movies, date), ct);
            EnsureTrustworthy(page, date, watch);
            showtimes.AddRange(page.Showtimes.Where(s => InScope(watch, s)));
        }

        Console.WriteLine($"[{watch.Name}] dates: {string.Join(", ", datesInScope.Select(d => d.ToString("dd MMM", CultureInfo.InvariantCulture)))}" +
                          $" | showtimes: {showtimes.Count} | bookable: {showtimes.Count(s => s.Bookable)}");

        var isNew = !state.Watches.TryGetValue(watch.Key, out var watchState);
        watchState ??= new WatchState();
        state.Watches[watch.Key] = watchState;

        var changes = ChangeDetector.Detect(watchState, datesInScope, datesInScope, showtimes, now, _nowLocal, state.SellOuts);
        string Url(DateOnly? d) => VoxUrls.Showtimes(config.Site, watch.Cinemas, watch.Movies, d).ToString();

        if (isNew)
        {
            await notifier.SendAsync(MessageComposer.Baseline(watch, datesInScope.Count, showtimes, warnings, Url), ct);
            return;
        }

        foreach (var c in changes)
        {
            log.Add(Line(c.Kind.ToString(), watch.Name, c.Showtime?.MovieTitle ?? "", c.Date, c.Showtime?.Cinema ?? "",
                c.Showtime?.Experience ?? "", c.Showtime?.Time, c.Showtime?.Id ?? ""));
            Console.WriteLine($"  {c.Kind,-14} {c.Date:ddd dd MMM} {c.Showtime?.Cinema} {c.Showtime?.Experience} {c.Showtime?.Time}");
        }

        var opened = changes.Where(c => c.Kind == ChangeKind.BookingOpened && c.Showtime is not null).Select(c => c.Showtime!).ToList();
        foreach (var byMovie in opened.GroupBy(s => s.MovieTitle))
            state.Openings.Add(new OpeningRecord(now, watch.Name, byMovie.Key, byMovie.Count(), byMovie.Select(s => s.Date).Distinct().Order().ToList()));
        if (state.Openings.Count > 300) state.Openings.RemoveRange(0, state.Openings.Count - 300);
        if (state.SellOuts.Count > 500) state.SellOuts.RemoveRange(0, state.SellOuts.Count - 500);

        foreach (var n in MessageComposer.Compose(watch, changes, Url))
            await notifier.SendAsync(n, ct);
    }

    /// <summary>
    /// A glitchy page must never reach the detector. If it did, its showtimes would be
    /// dropped from state and announced again as new (or BOOK NOW) on the next good run.
    /// <paramref name="requested"/> is null for the first page, which has no date in its URL.
    /// </summary>
    private static void EnsureTrustworthy(ShowtimesPage page, DateOnly? requested, WatchDefinition watch)
    {
        var which = requested is { } r ? $"The page for {r:ddd dd MMM}" : "The showtimes page";
        if (!page.LooksValid)
            throw new VoxSiteException($"[{watch.Name}] {which} did not look like a VOX showtimes page. VOX may have changed the layout.");
        if (requested is { } date && page.PageDate != date)
            throw new VoxSiteException($"[{watch.Name}] Asked for {date:dd MMM} but the page is for {page.PageDate:dd MMM}.");
        if (page.Showtimes.Count == 0 && !page.HasNoShowtimesMessage)
            throw new VoxSiteException($"[{watch.Name}] {which} had no showtimes and no 'no showtimes' message.");
    }

    private bool InScope(WatchDefinition watch, Showtime s)
    {
        // The site already filters by c and m. We filter again so an unknown slug never floods you with every movie.
        if (watch.Movies.Count > 0 && !watch.Movies.Contains(s.MovieSlug, StringComparer.OrdinalIgnoreCase)) return false;

        if (watch.Cinemas.Count > 0)
        {
            var names = _cinemas.Where(c => watch.Cinemas.Contains(c.Slug, StringComparer.OrdinalIgnoreCase)).Select(c => c.Name);
            if (!names.Contains(s.Cinema, StringComparer.OrdinalIgnoreCase)) return false;
        }

        return watch.Matches(s);
    }

    private List<string> Validate(WatchDefinition watch)
    {
        var warnings = new List<string>();
        foreach (var c in watch.Cinemas.Where(c => !_cinemas.Any(x => x.Slug.Equals(c, StringComparison.OrdinalIgnoreCase))))
            warnings.Add($"Unknown cinema \"{c}\". See state/CATALOG.md for valid slugs.");
        foreach (var m in watch.Movies.Where(m => !_movies.Any(x => x.Slug.Equals(m, StringComparison.OrdinalIgnoreCase))))
            warnings.Add($"Movie \"{m}\" is not on the site yet. You will get an alert when it appears.");
        return warnings;
    }

    private async Task<ShowtimesPage> FetchAsync(Uri url, CancellationToken ct)
    {
        var html = await source.GetAsync(url, ct);
        return ShowtimesParser.Parse(html, url, Today);
    }

    private async Task RecordFailureAsync(WatcherState state, string error, CancellationToken ct)
    {
        var health = state.Health;
        health.ConsecutiveFailures++;
        health.FirstFailureAt ??= now;
        health.LastError = error;
        Console.WriteLine($"FAILED ({health.ConsecutiveFailures} in a row): {error}");

        // Each job checks several times, so 3 failures can happen within minutes. Only alert for a lasting problem.
        var failingFor = now - health.FirstFailureAt.Value;
        if (health.ConsecutiveFailures >= HealthAlertAfterFailures && failingFor >= HealthAlertAfter && !health.Alerted)
        {
            health.Alerted = true;
            await notifier.SendAsync(new Notification("VOX watcher has a problem",
                $"{health.ConsecutiveFailures} checks failed in a row over {failingFor.TotalMinutes:0} minutes.\n{error}\nYou will get a message when it works again.",
                4, ["warning"]), ct);
        }
    }

    private async Task RecoverHealthAsync(WatcherState state, CancellationToken ct)
    {
        if (state.Health.Alerted)
            await notifier.SendAsync(new Notification("VOX watcher is working again", "Checks are back to normal.", 2, ["white_check_mark"]), ct);

        state.Health = new HealthState();
    }

    private string Line(string kind, string watch, string movie, DateOnly? date, string cinema, string exp, TimeOnly? time, string id)
    {
        var local = TimeZoneInfo.ConvertTime(now, tz);
        return string.Join(" | ",
            local.ToString("yyyy-MM-dd ddd HH:mm", CultureInfo.InvariantCulture), kind, watch, movie,
            date?.ToString("ddd dd MMM", CultureInfo.InvariantCulture) ?? "", cinema, exp,
            time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "", id);
    }
}

public static class VoxUrls
{
    public static Uri Showtimes(string site, IReadOnlyList<string> cinemas, IReadOnlyList<string> movies, DateOnly? date)
    {
        var parts = cinemas.Select(c => "c=" + Uri.EscapeDataString(c))
            .Concat(movies.Select(m => "m=" + Uri.EscapeDataString(m)))
            .ToList();
        if (date is { } d) parts.Add("d=" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        var query = parts.Count > 0 ? "?" + string.Join('&', parts) : "";
        return new Uri(site.TrimEnd('/') + "/showtimes" + query);
    }
}
