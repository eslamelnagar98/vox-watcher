using System.Globalization;
using System.Text;
using VoxWatcher.Configuration;
using VoxWatcher.Domain;

namespace VoxWatcher.Notifications;

/// <summary>Turns raw changes into a small number of clear, grouped push messages.</summary>
public static class MessageComposer
{
    private const int MaxLines = 12;

    public static List<Notification> Compose(WatchDefinition watch, IReadOnlyList<Change> changes, Func<DateOnly?, string> showtimesUrl)
    {
        var result = new List<Notification>();
        var a = watch.Alerts;

        var opened = changes.Where(c => c.Showtime is not null &&
            ((c.Kind == ChangeKind.BookingOpened && a.BookingOpened) || (c.Kind == ChangeKind.SeatsReturned && a.SeatsReturned)))
            .Select(c => (c.Showtime!, Returned: c.Kind == ChangeKind.SeatsReturned)).ToList();

        var listed = a.NewShowtimes
            ? changes.Where(c => c.Kind == ChangeKind.SessionListed && c.Showtime is not null).Select(c => c.Showtime!).ToList()
            : [];

        var soldOut = a.SoldOut
            ? changes.Where(c => c.Kind == ChangeKind.SoldOut && c.Showtime is not null).Select(c => c.Showtime!).ToList()
            : [];

        if (opened.Count > 0)
        {
            var first = opened.Select(o => o.Item1).OrderBy(s => s.Date).ThenBy(s => s.Time).First();
            var onlyReturned = opened.All(o => o.Returned);
            var title = (onlyReturned ? "Seats are back: " : "BOOK NOW: ") + Movies(opened.Select(o => o.Item1));

            var body = new StringBuilder();
            body.AppendLine(onlyReturned
                ? "Seats were released on sessions that were sold out."
                : $"{opened.Count} showtime(s) just became bookable. Be quick.");
            body.Append(GroupLines(opened.Select(o => o.Item1), s => opened.Any(o => o.Returned && o.Item1.Id == s.Id) ? " (seats back)" : ""));

            if (listed.Count > 0)
            {
                body.AppendLine();
                body.Append($"Also listed but not bookable yet: {listed.Count} showtime(s) on {DateList(listed.Select(s => s.Date))}.");
            }

            var actions = new List<NotificationAction>();
            if (first.BookingUrl is not null)
                actions.Add(new($"Book {first.Date:ddd dd} {Time(first.Time)}", first.BookingUrl));
            actions.Add(new("All showtimes", showtimesUrl(first.Date)));

            result.Add(new Notification(title, body.ToString().TrimEnd(), 5,
                ["rotating_light", "admission_tickets"], first.BookingUrl ?? showtimesUrl(first.Date), actions));
        }
        else if (listed.Count > 0)
        {
            var first = listed.OrderBy(s => s.Date).ThenBy(s => s.Time).First();
            var body = new StringBuilder();
            body.AppendLine($"{listed.Count} new showtime(s) listed. Not bookable yet.");
            body.Append(GroupLines(listed, _ => ""));
            body.AppendLine();
            body.Append("You will get an urgent alert the moment booking opens.");

            result.Add(new Notification($"New showtimes: {Movies(listed)}", body.ToString().TrimEnd(), 3,
                ["date"], showtimesUrl(first.Date), [new("Open showtimes", showtimesUrl(first.Date))]));
        }

        if (soldOut.Count > 0)
        {
            result.Add(new Notification($"Sold out: {Movies(soldOut)}", GroupLines(soldOut, _ => "").TrimEnd(), 2,
                ["no_entry"], showtimesUrl(soldOut.Min(s => s.Date))));
        }

        return result;
    }

    public static Notification Baseline(WatchDefinition watch, int dates, IReadOnlyList<Showtime> showtimes, IReadOnlyList<string> warnings, Func<DateOnly?, string> showtimesUrl)
    {
        var bookable = showtimes.Where(s => s.Bookable).ToList();
        var body = new StringBuilder();
        body.AppendLine($"Cinemas: {(watch.Cinemas.Count == 0 ? "all" : string.Join(", ", watch.Cinemas))}");
        body.AppendLine($"Movies: {(watch.Movies.Count == 0 ? "all" : string.Join(", ", watch.Movies))}");
        body.AppendLine($"Right now: {dates} date(s), {showtimes.Count} matching showtime(s), {bookable.Count} bookable.");

        if (bookable.Count > 0)
        {
            body.AppendLine("Already bookable:");
            body.Append(GroupLines(bookable, _ => ""));
        }

        foreach (var w in warnings) body.AppendLine($"Warning: {w}");
        body.Append("From now on you only get alerts for changes.");

        return new Notification($"Watching: {watch.Name}", body.ToString().TrimEnd(), bookable.Count > 0 ? 4 : 2,
            ["eyes"], showtimesUrl(null));
    }

    private static string GroupLines(IEnumerable<Showtime> showtimes, Func<Showtime, string> suffix)
    {
        var lines = showtimes
            .GroupBy(s => (s.Date, s.Cinema, s.Experience, s.MovieTitle))
            .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Cinema).ThenBy(g => g.Key.Experience)
            .Select(g =>
            {
                var times = string.Join(", ", g.OrderBy(s => s.Time).Select(s => Time(s.Time) + suffix(s)));
                return $"{g.Key.Date:ddd dd MMM} | {Short(g.Key.Cinema)} | {g.Key.Experience}: {times}";
            })
            .ToList();

        var sb = new StringBuilder();
        foreach (var line in lines.Take(MaxLines)) sb.AppendLine(line);
        if (lines.Count > MaxLines) sb.AppendLine($"+{lines.Count - MaxLines} more");
        return sb.ToString();
    }

    private static string Movies(IEnumerable<Showtime> showtimes) =>
        string.Join(", ", showtimes.Select(s => s.MovieTitle).Distinct().Take(3));

    private static string DateList(IEnumerable<DateOnly> dates)
    {
        var d = dates.Distinct().Order().ToList();
        return string.Join(", ", d.Take(5).Select(x => x.ToString("ddd dd MMM", CultureInfo.InvariantCulture))) + (d.Count > 5 ? $" +{d.Count - 5}" : "");
    }

    private static string Time(TimeOnly t) => t.ToString("h:mmtt", CultureInfo.InvariantCulture).ToLowerInvariant();

    private static string Short(string cinema) => cinema.Replace("City Centre ", "CC ", StringComparison.OrdinalIgnoreCase);
}
