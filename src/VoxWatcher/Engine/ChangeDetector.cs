using VoxWatcher.Domain;
using VoxWatcher.State;

namespace VoxWatcher.Engine;

/// <summary>
/// Compares what the site shows now with what we saw last time.
/// The key signal: a session that was listed as "unavailable" turns into a bookable link.
/// </summary>
public static class ChangeDetector
{
    public static List<Change> Detect(
        WatchState state,
        IReadOnlyCollection<DateOnly> datesNow,
        IReadOnlyCollection<DateOnly> fetchedDates,
        IReadOnlyList<Showtime> showtimesNow,
        DateTimeOffset now,
        DateTime nowLocal,
        List<SellOutRecord> sellOuts)
    {
        var changes = new List<Change>();
        var today = DateOnly.FromDateTime(nowLocal);

        foreach (var date in datesNow.Where(d => !state.Dates.Contains(d)).Order())
            changes.Add(new Change(ChangeKind.DateAdded, date));

        var seenNow = new HashSet<string>();

        foreach (var s in showtimesNow.OrderBy(x => x.Date).ThenBy(x => x.Time))
        {
            if (!seenNow.Add(s.Id)) continue;

            if (!state.Sessions.TryGetValue(s.Id, out var prev))
            {
                state.Sessions[s.Id] = new SessionState
                {
                    Movie = s.MovieTitle,
                    Cinema = s.Cinema,
                    Experience = s.Experience,
                    Date = s.Date,
                    Time = s.Time,
                    Bookable = s.Bookable,
                    EverBookable = s.Bookable,
                    FirstSeen = now,
                    OpenedAt = s.Bookable ? now : null
                };
                changes.Add(new Change(s.Bookable ? ChangeKind.BookingOpened : ChangeKind.SessionListed, s.Date, s));
                continue;
            }

            if (!prev.Bookable && s.Bookable)
            {
                changes.Add(new Change(prev.EverBookable ? ChangeKind.SeatsReturned : ChangeKind.BookingOpened, s.Date, s));
                prev.OpenedAt ??= now;
                prev.EverBookable = true;
                prev.SoldOutAt = null;
            }
            else if (prev.Bookable && !s.Bookable && HasStarted(s, nowLocal))
            {
                // The show already started, so VOX closed sales. That is not a sell-out.
                prev.SoldOutAt = null;
            }
            else if (prev.Bookable && !s.Bookable)
            {
                changes.Add(new Change(ChangeKind.SoldOut, s.Date, s));
                prev.SoldOutAt = now;
                if (prev.OpenedAt is { } opened)
                    sellOuts.Add(new SellOutRecord(s.MovieTitle, s.Cinema, s.Experience, s.Date, Math.Round((now - opened).TotalMinutes, 1)));
            }

            prev.Bookable = s.Bookable;
            prev.Movie = s.MovieTitle;
            prev.Time = s.Time;
        }

        // Sessions that disappeared from a date we did check: forget them.
        foreach (var id in state.Sessions.Where(kv => fetchedDates.Contains(kv.Value.Date) && !seenNow.Contains(kv.Key)).Select(kv => kv.Key).ToList())
            state.Sessions.Remove(id);

        // Forget the past.
        foreach (var id in state.Sessions.Where(kv => kv.Value.Date < today).Select(kv => kv.Key).ToList())
            state.Sessions.Remove(id);

        state.Dates = new SortedSet<DateOnly>(datesNow.Where(d => d >= today));
        return changes;
    }

    /// <summary>Start time (local, e.g. Cairo) is at or before now.</summary>
    private static bool HasStarted(Showtime s, DateTime nowLocal) => s.Date.ToDateTime(s.Time) <= nowLocal;
}
