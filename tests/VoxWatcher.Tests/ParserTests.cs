using System.Globalization;
using VoxWatcher.Domain;
using VoxWatcher.Vox;

namespace VoxWatcher.Tests;

/// <summary>
/// The parser against real pages from egy.voxcinemas.com, captured on Thu 26 Nov 2026
/// (only scripts, styles, head and footer removed; the showtimes markup is untouched).
/// </summary>
public sealed class ParserTests
{
    private const string Site = "https://egy.voxcinemas.com/showtimes";
    private static readonly DateOnly CaptureDay = new(2026, 11, 26);

    [Fact]
    public void Movie_page_reads_date_tabs_and_every_showtime()
    {
        var page = Parse("almaza-doomsday.html", "?c=city-centre-almaza&m=avengers-doomsday");

        Assert.Equal(new DateOnly(2026, 12, 16), page.PageDate);
        Assert.Equal(Enumerable.Range(16, 8).Select(d => new DateOnly(2026, 12, d)), page.Dates);
        Assert.Equal(12, page.Showtimes.Count);
        Assert.Equal(8, page.Showtimes.Count(s => s.Experience == "GOLD"));
        Assert.Equal(4, page.Showtimes.Count(s => s.Experience == "Standard"));
        Assert.All(page.Showtimes, s =>
        {
            Assert.False(s.Bookable);
            Assert.Null(s.BookingUrl);
            Assert.Equal("Avengers: Doomsday", s.MovieTitle);
            Assert.Equal("avengers-doomsday", s.MovieSlug);
            Assert.Equal("City Centre Almaza", s.Cinema);
            Assert.Equal(new DateOnly(2026, 12, 16), s.Date);
        });

        var first = page.Showtimes.Single(s => s.Id == "0047-276882");
        Assert.Equal(("GOLD", new TimeOnly(11, 15)), (first.Experience, first.Time));
    }

    [Fact]
    public void Date_page_tells_bookable_from_unavailable()
    {
        var page = Parse("almaza-doomsday-20261220.html", "?c=city-centre-almaza&m=avengers-doomsday&d=20261220");

        Assert.Equal(new DateOnly(2026, 12, 20), page.PageDate);
        Assert.Equal(new[] { "0047-276996", "0047-276997", "0047-276999" }, page.Showtimes.Where(s => s.Bookable).Select(s => s.Id));

        var bookable = page.Showtimes.Single(s => s.Id == "0047-276996");
        Assert.Equal(("Standard", new TimeOnly(11, 45)), (bookable.Experience, bookable.Time));
        Assert.Equal("https://egy.voxcinemas.com/booking/0047-276996", bookable.BookingUrl);

        var unavailable = page.Showtimes.Single(s => s.Id == "0047-276998");
        Assert.False(unavailable.Bookable);
        Assert.Equal(new TimeOnly(19, 15), unavailable.Time);
    }

    [Fact]
    public void Date_without_shows_has_the_message_and_ignores_suggested_movies()
    {
        var page = Parse("almaza-doomsday-20270301.html", "?c=city-centre-almaza&m=avengers-doomsday&d=20270301");

        Assert.Equal(new DateOnly(2027, 3, 1), page.PageDate);
        Assert.True(page.HasNoShowtimesMessage);
        Assert.True(page.LooksValid);
        Assert.Empty(page.Showtimes);
        Assert.Equal(8, page.Dates.Count);
    }

    [Fact]
    public void Showtimes_home_page_has_the_catalog_and_tabs_with_gaps()
    {
        var page = Parse("catalog.html", "");

        Assert.Equal(CaptureDay, page.PageDate); // the "Today" tab
        Assert.Equal(16, page.Dates.Count);       // VOX lists every date that has shows, with gaps, not a fixed window
        Assert.Contains(CaptureDay.AddDays(1), page.Dates);
        Assert.Contains(new DateOnly(2026, 12, 29), page.Dates);

        Assert.Equal(3, page.Cinemas.Count);
        Assert.Contains(page.Cinemas, c => c is { Slug: "city-centre-almaza", Name: "City Centre Almaza" });
        Assert.Equal(22, page.Movies.Count);
        Assert.Contains(page.Movies, m => m is { Slug: "avengers-doomsday", Name: "Avengers: Doomsday" });

        Assert.Equal(121, page.Showtimes.Count);
        Assert.Equal(16, page.Showtimes.Select(s => s.MovieSlug).Distinct().Count());
    }

    [Theory]
    [InlineData("Today", "2026-11-26", "2026-11-26")]
    [InlineData("Tomorrow", "2026-11-26", "2026-11-27")]
    [InlineData("Wed 16 Dec", "2026-11-26", "2026-12-16")]
    [InlineData("Fri 01 Jan", "2026-12-20", "2027-01-01")]
    public void Tab_text_becomes_a_date_with_the_right_year(string tab, string today, string expected) =>
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture),
            ShowtimesParser.ParseTabText(tab, DateOnly.Parse(today, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("11:45am ", 11, 45)]
    [InlineData("3:30pm", 15, 30)]
    [InlineData("12:00am", 0, 0)]
    public void Showtime_text_becomes_a_time(string text, int hour, int minute)
    {
        Assert.True(ShowtimesParser.TryParseTime(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    private static ShowtimesPage Parse(string file, string query) =>
        ShowtimesParser.Parse(Fixtures.Read(Path.Combine("Real", file)), new Uri(Site + query), CaptureDay);
}
