using System.Globalization;
using System.Text;
using VoxWatcher.Configuration;
using VoxWatcher.Domain;
using VoxWatcher.State;

namespace VoxWatcher.Engine;

/// <summary>Builds state/INSIGHTS.md: when VOX opens bookings, and how fast sessions sell out.</summary>
public static class InsightsWriter
{
    public static string Build(WatcherState state, WatchConfig config, TimeZoneInfo tz, DateOnly today)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# VOX Watcher insights");
        sb.AppendLine();
        sb.AppendLine("This file is rebuilt by the watcher. Times are in " + config.TimeZone + ".");
        sb.AppendLine();

        // 1. Current status
        sb.AppendLine("## Current status");
        sb.AppendLine();
        sb.AppendLine("| Watch | Dates listed | Showtimes | Bookable now | Listed, not bookable |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var w in config.Watches.Where(w => w.Enabled))
        {
            if (!state.Watches.TryGetValue(w.Key, out var ws)) { sb.AppendLine($"| {w.Name} | not checked yet | | | |"); continue; }
            var sessions = ws.Sessions.Values.Where(s => s.Date >= today).ToList();
            var dates = ws.Dates.Count == 0 ? "none" : string.Join(", ", ws.Dates.Select(d => d.ToString("dd MMM", CultureInfo.InvariantCulture)));
            sb.AppendLine($"| {w.Name} | {dates} | {sessions.Count} | {sessions.Count(s => s.Bookable)} | {sessions.Count(s => !s.Bookable)} |");
        }
        sb.AppendLine();

        // 2. Opening pattern
        sb.AppendLine("## When does VOX open bookings?");
        sb.AppendLine();
        if (state.Openings.Count == 0)
        {
            sb.AppendLine("No booking openings recorded yet. After the first few, this section shows the day and hour pattern.");
        }
        else
        {
            var local = state.Openings.Select(o => (o, At: TimeZoneInfo.ConvertTime(o.At, tz))).ToList();

            var byDay = local.GroupBy(x => x.At.DayOfWeek).OrderByDescending(g => g.Count()).ToList();
            var topDay = byDay[0];
            var hours = topDay.Select(x => x.At.Hour).ToList();
            sb.AppendLine($"**Most common:** {topDay.Key}s, between {hours.Min():00}:00 and {hours.Max():00}:59 ({topDay.Count()} of {local.Count} openings).");
            sb.AppendLine();
            sb.AppendLine("Note: the watcher checks about every 2 to 3 minutes, so each time is accurate to a few minutes.");
            sb.AppendLine();
            sb.AppendLine("| Day | Openings |");
            sb.AppendLine("|---|---|");
            foreach (var g in byDay) sb.AppendLine($"| {g.Key} | {g.Count()} |");
            sb.AppendLine();
            sb.AppendLine("| Detected at | Movie | Showtimes opened | For dates |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var (o, at) in local.OrderByDescending(x => x.At).Take(30))
            {
                var dates = string.Join(", ", o.Dates.Distinct().Order().Select(d => d.ToString("dd MMM", CultureInfo.InvariantCulture)));
                sb.AppendLine($"| {at:ddd dd MMM yyyy HH:mm} | {o.Movie} | {o.Sessions} | {dates} |");
            }
        }
        sb.AppendLine();

        // 3. Sell-out speed
        sb.AppendLine("## How fast do showtimes sell out?");
        sb.AppendLine();
        if (state.SellOuts.Count == 0)
        {
            sb.AppendLine("No sell-outs recorded yet.");
        }
        else
        {
            sb.AppendLine("| Experience | Sell-outs | Median minutes bookable | Fastest |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var g in state.SellOuts.GroupBy(s => s.Experience).OrderBy(g => Median(g.Select(x => x.MinutesBookable))))
            {
                var mins = g.Select(x => x.MinutesBookable).ToList();
                sb.AppendLine($"| {g.Key} | {mins.Count} | {Median(mins):0} | {mins.Min():0} |");
            }
        }

        return sb.ToString();
    }

    public static string BuildCatalog(IReadOnlyList<CatalogItem> cinemas, IReadOnlyList<CatalogItem> movies, string site)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# VOX catalog");
        sb.AppendLine();
        sb.AppendLine($"Copy these slugs into `watches.json`. Source: {site}/showtimes. Rebuilt automatically.");
        sb.AppendLine();
        sb.AppendLine("## Cinemas");
        sb.AppendLine();
        sb.AppendLine("| Slug | Name |");
        sb.AppendLine("|---|---|");
        foreach (var c in cinemas.OrderBy(c => c.Name)) sb.AppendLine($"| `{c.Slug}` | {c.Name} |");
        sb.AppendLine();
        sb.AppendLine("## Movies currently on the site");
        sb.AppendLine();
        sb.AppendLine("| Slug | Title |");
        sb.AppendLine("|---|---|");
        foreach (var m in movies.OrderBy(m => m.Name)) sb.AppendLine($"| `{m.Slug}` | {m.Name} |");
        return sb.ToString();
    }

    private static double Median(IEnumerable<double> values)
    {
        var v = values.Order().ToList();
        if (v.Count == 0) return 0;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }
}
