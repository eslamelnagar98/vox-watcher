namespace VoxWatcher.Tests;

/// <summary>End-to-end runs of the watcher against a fake VOX site, step by step, with real state between runs.</summary>
public sealed class ScenarioTests : IDisposable
{
    private const string Baseline = "2026-11-26T10:00:00Z";
    private const string BookingOpens = "2026-11-30T09:05:00Z";

    private static readonly FakeVoxSite Normal = FakeVoxSite.Create();
    private static readonly FakeVoxSite Gold715Open = FakeVoxSite.Create(gold715Bookable: true, add24Dec: true);
    private static readonly FakeVoxSite Gold715Closed = FakeVoxSite.Create(add24Dec: true);

    private readonly Scenario _s = new();

    public void Dispose() => _s.Dispose();

    [Fact]
    public async Task First_run_sends_one_watching_summary()
    {
        var sent = await _s.RunAsync(Normal, Baseline);

        var n = Assert.Single(sent);
        Assert.Equal("Watching: Doomsday at Almaza", n.Title);
        Assert.Equal(4, n.Priority); // something is already bookable
        Assert.Contains("Sun 20 Dec | CC Almaza | Standard: 3:30pm, 11:00pm", n.Message); // 11:45am is before time.after
    }

    [Fact]
    public async Task Booking_opening_sends_book_now_with_a_direct_booking_link()
    {
        await _s.RunAsync(Normal, Baseline);

        var sent = await _s.RunAsync(Gold715Open, BookingOpens);

        var n = Assert.Single(sent);
        Assert.Equal("BOOK NOW: Avengers: Doomsday", n.Title);
        Assert.Equal(5, n.Priority);
        Assert.Contains("Fri 18 Dec | CC Almaza | GOLD: 7:15pm", n.Message);
        Assert.Contains("Also listed but not bookable yet", n.Message); // the new 24 Dec date
        Assert.Equal("https://egy.voxcinemas.com/booking/0047-276906", n.Click);
    }

    [Fact]
    public async Task Sell_out_is_recorded_but_not_pushed_by_default()
    {
        await _s.RunAsync(Normal, Baseline);
        await _s.RunAsync(Gold715Open, BookingOpens);

        var sent = await _s.RunAsync(Gold715Closed, "2026-11-30T09:47:00Z");

        Assert.Empty(sent);
        Assert.Equal(42, Assert.Single(_s.State.SellOuts).MinutesBookable);
        Assert.Contains(_s.Events, l => l.Contains("| SoldOut |") && l.EndsWith("0047-276906", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seats_coming_back_after_a_sell_out_sends_seats_are_back()
    {
        await _s.RunAsync(Normal, Baseline);
        await _s.RunAsync(Gold715Open, BookingOpens);
        await _s.RunAsync(Gold715Closed, "2026-11-30T09:47:00Z");

        var sent = await _s.RunAsync(Gold715Open, "2026-11-30T13:20:00Z");

        Assert.Equal("Seats are back: Avengers: Doomsday", Assert.Single(sent).Title);
    }

    [Theory]
    [InlineData(20, false, "Sun 20 Dec")]               // one date page is broken
    [InlineData(null, true, "The showtimes page")]      // the first page has tabs but no showtimes and no message
    public async Task A_broken_page_fails_the_check_and_never_causes_repeat_alerts(int? brokenDate, bool brokenFirstPage, string errorMentions)
    {
        await _s.RunAsync(Normal, Baseline);
        var sessionsBefore = _s.SessionIds;

        var sent = await _s.RunAsync(FakeVoxSite.Create(brokenDate: brokenDate, brokenFirstPage: brokenFirstPage), "2026-11-30T09:00:00Z");

        Assert.Empty(sent);
        Assert.Equal(1, _s.State.Health.ConsecutiveFailures);
        Assert.Contains(errorMentions, _s.State.Health.LastError);
        Assert.Equal(sessionsBefore, _s.SessionIds);

        // The next good check must not announce the same showtimes again.
        sent = await _s.RunAsync(Normal, "2026-11-30T09:05:00Z");

        Assert.Empty(sent);
        Assert.Equal(sessionsBefore, _s.SessionIds);
        Assert.DoesNotContain(_s.Events, l => l.Contains("| SessionListed |") || l.Contains("| BookingOpened |"));
    }

    [Fact]
    public async Task Problem_alert_waits_until_checks_have_failed_for_15_minutes_and_is_sent_once()
    {
        await _s.RunAsync(Normal, Baseline);

        foreach (var time in new[] { "14:00", "14:01", "14:02" })
            Assert.Empty(await _s.RunAsync(FakeVoxSite.Down(), $"2026-11-30T{time}:00Z"));

        var alert = Assert.Single(await _s.RunAsync(FakeVoxSite.Down(), "2026-11-30T14:15:00Z"));
        Assert.Equal("VOX watcher has a problem", alert.Title);
        Assert.Contains("4 checks failed in a row over 15 minutes", alert.Message);

        Assert.Empty(await _s.RunAsync(FakeVoxSite.Down(), "2026-11-30T14:20:00Z"));

        var recovered = Assert.Single(await _s.RunAsync(Normal, "2026-11-30T14:25:00Z"));
        Assert.Equal("VOX watcher is working again", recovered.Title);
    }

    [Fact]
    public async Task A_short_outage_sends_nothing()
    {
        await _s.RunAsync(Normal, Baseline);

        foreach (var time in new[] { "14:00", "14:01", "14:02" })
            Assert.Empty(await _s.RunAsync(FakeVoxSite.Down(), $"2026-11-30T{time}:00Z"));
        Assert.Empty(await _s.RunAsync(Normal, "2026-11-30T14:04:00Z"));

        Assert.Equal(0, _s.State.Health.ConsecutiveFailures);
        Assert.Null(_s.State.Health.FirstFailureAt);
    }

    [Fact]
    public async Task A_show_that_already_started_is_not_a_sell_out()
    {
        await _s.RunAsync(Normal, Baseline);
        await _s.RunAsync(Gold715Open, BookingOpens);

        // Fri 18 Dec 19:30 Cairo (17:30 UTC): the 7:15pm GOLD show has started, so VOX closed it.
        var sent = await _s.RunAsync(Gold715Closed, "2026-12-18T17:30:00Z");

        Assert.Empty(sent);
        Assert.Empty(_s.State.SellOuts);
        Assert.DoesNotContain(_s.Events, l => l.Contains("| SoldOut |"));
    }
}
