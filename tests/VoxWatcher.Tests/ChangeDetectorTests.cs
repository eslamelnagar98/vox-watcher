using System.Globalization;
using VoxWatcher.Domain;
using VoxWatcher.Engine;
using VoxWatcher.State;

namespace VoxWatcher.Tests;

public sealed class ChangeDetectorTests
{
    private static readonly DateOnly Fri18 = new(2026, 12, 18);

    private readonly WatchState _state = new();
    private readonly List<SellOutRecord> _sellOuts = [];

    [Fact]
    public void A_session_is_listed_then_opens_then_sells_out_then_gets_seats_back()
    {
        Assert.Equal(new[] { ChangeKind.SessionListed }, Detect("2026-11-30T09:00", Show("1", bookable: false)));
        Assert.Equal(new[] { ChangeKind.BookingOpened }, Detect("2026-11-30T09:05", Show("1", bookable: true)));
        Assert.Equal(new[] { ChangeKind.SoldOut }, Detect("2026-11-30T09:35", Show("1", bookable: false)));
        Assert.Equal(new[] { ChangeKind.SeatsReturned }, Detect("2026-11-30T13:00", Show("1", bookable: true)));

        Assert.Equal(30, Assert.Single(_sellOuts).MinutesBookable);
    }

    [Theory]
    [InlineData("2026-12-18", "19:15", "2026-12-18T19:00", true)]   // closed before it starts: sold out
    [InlineData("2026-12-18", "19:15", "2026-12-18T19:30", false)]  // closed after it started
    [InlineData("2026-12-20", "00:00", "2026-12-20T14:00", true)]   // after-midnight show is listed under the previous day
    [InlineData("2026-12-20", "00:00", "2026-12-21T00:10", false)]  // ... and starts the next night
    public void A_bookable_session_closing_is_a_sell_out_only_before_it_starts(string date, string time, string closedAtLocal, bool soldOut)
    {
        var d = DateOnly.Parse(date, CultureInfo.InvariantCulture);
        var t = TimeOnly.Parse(time, CultureInfo.InvariantCulture);
        Detect("2026-12-18T09:00", Show("1", bookable: true, d, t));

        var changes = Detect(closedAtLocal, Show("1", bookable: false, d, t));

        Assert.Equal(soldOut, changes.Contains(ChangeKind.SoldOut));
        Assert.Equal(soldOut ? 1 : 0, _sellOuts.Count);
    }

    [Fact]
    public void Sessions_missing_from_a_checked_date_are_forgotten_but_other_dates_are_kept()
    {
        var sat19 = Fri18.AddDays(1);
        Detect("2026-11-30T09:00", Show("fri", bookable: false), Show("sat", bookable: false, sat19));

        // Only Friday was checked this time, and its session is gone.
        ChangeDetector.Detect(_state, [Fri18, sat19], [Fri18], [], At("2026-11-30T09:05"), Local("2026-11-30T09:05"), _sellOuts);

        Assert.Equal(new[] { "sat" }, _state.Sessions.Keys);
    }

    private List<ChangeKind> Detect(string local, params Showtime[] shows)
    {
        var dates = shows.Select(s => s.Date).Distinct().ToList();
        return ChangeDetector.Detect(_state, dates, dates, shows, At(local), Local(local), _sellOuts)
            .Where(c => c.Showtime is not null)
            .Select(c => c.Kind)
            .ToList();
    }

    private static Showtime Show(string id, bool bookable, DateOnly? date = null, TimeOnly? time = null) =>
        new(id, "avengers-doomsday", "Avengers: Doomsday", "City Centre Almaza", "GOLD",
            date ?? Fri18, time ?? new TimeOnly(19, 15), bookable,
            bookable ? $"https://egy.voxcinemas.com/booking/{id}" : null);

    private static DateTime Local(string local) => DateTime.Parse(local, CultureInfo.InvariantCulture);

    // Cairo is UTC+2 in winter.
    private static DateTimeOffset At(string local) => new(Local(local), TimeSpan.FromHours(2));
}
