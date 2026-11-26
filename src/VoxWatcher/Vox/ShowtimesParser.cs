using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using VoxWatcher.Domain;

namespace VoxWatcher.Vox;

/// <summary>
/// Reads the server-rendered HTML of /showtimes.
/// Built against the real markup:
///   nav.date-filter li > span (selected date) | a[href*="d=YYYYMMDD"]
///   article.movie-compare[data-slug] > h2 (title), h3.highlight (cinema),
///   ol.showtimes li > strong (experience) + li[data-id] > a.showtime (bookable) | span.showtime.unavailable
///   input[name=c|m] + span (catalog)
/// </summary>
public static partial class ShowtimesParser
{
    [GeneratedRegex(@"<nav\s+class=""date-filter""\s*>(?<body>[\s\S]*?)</nav>")]
    private static partial Regex NavBlock();

    [GeneratedRegex(@"<li>\s*(?:<span>(?<selected>[^<]+)</span>|<a\s+href=""(?<href>[^""]+)""[^>]*>(?<text>[^<]+)</a>)\s*</li>")]
    private static partial Regex NavItem();

    [GeneratedRegex(@"[?&]d=(?<d>\d{8})")]
    private static partial Regex DateParam();

    [GeneratedRegex(@"<input[^>]*\bname=""(?<kind>[cm])""[^>]*\bvalue=""(?<slug>[^""]+)""[^>]*>\s*<span>(?<name>[^<]*)</span>")]
    private static partial Regex CatalogInput();

    [GeneratedRegex(@"<article\s+class=""movie-compare""[^>]*?data-slug=""(?<slug>[^""]+)""")]
    private static partial Regex Article();

    [GeneratedRegex(
        @"<h2>(?<title>[^<]+)</h2>" +
        @"|<h3\s+class=""highlight"">(?<cinema>[^<]+)</h3>" +
        @"|<strong>(?<exp>[^<]+)</strong>" +
        @"|<li\s+data-id=""(?<id>[^""]+)"">\s*(?:<a\s+(?<aattrs>[^>]*)>|<span\s+class=""(?<sclass>[^""]*)"">)\s*(?<time>[^<]+?)\s*</(?:a|span)>")]
    private static partial Regex Token();

    [GeneratedRegex(@"\bhref=""(?<v>[^""]+)""")]
    private static partial Regex HrefAttr();

    [GeneratedRegex(@"\bclass=""(?<v>[^""]+)""")]
    private static partial Regex ClassAttr();

    [GeneratedRegex(@"No showtimes could be found", RegexOptions.IgnoreCase)]
    private static partial Regex NoShowtimes();

    public static ShowtimesPage Parse(string html, Uri pageUrl, DateOnly today)
    {
        var requestedDate = ReadDateParam(pageUrl.Query);
        var (dates, selected) = ParseDateNav(html, today);
        var pageDate = requestedDate ?? selected;

        var showtimes = pageDate is { } date ? ParseShowtimes(html, date, pageUrl) : [];
        var (cinemas, movies) = ParseCatalog(html);

        // The selected date is a <span>, not a link. Make sure it is in the list too.
        if (pageDate is { } pd && showtimes.Count > 0 && !dates.Contains(pd))
            dates = [.. dates.Append(pd).Order()];

        return new ShowtimesPage(pageDate, dates, showtimes, cinemas, movies, NoShowtimes().IsMatch(html));
    }

    private static (List<DateOnly> Dates, DateOnly? Selected) ParseDateNav(string html, DateOnly today)
    {
        var nav = NavBlock().Match(html);
        if (!nav.Success) return ([], null);

        var dates = new List<DateOnly>();
        DateOnly? selected = null;

        foreach (Match item in NavItem().Matches(nav.Groups["body"].Value))
        {
            if (item.Groups["selected"].Success)
            {
                selected = ParseTabText(Decode(item.Groups["selected"].Value), today);
                if (selected is { } s) dates.Add(s);
            }
            else if (ReadDateParam(Decode(item.Groups["href"].Value)) is { } d)
            {
                dates.Add(d);
            }
        }

        return (dates.Distinct().Order().ToList(), selected);
    }

    private static List<Showtime> ParseShowtimes(string html, DateOnly date, Uri pageUrl)
    {
        var result = new List<Showtime>();
        var articles = Article().Matches(html);

        for (var i = 0; i < articles.Count; i++)
        {
            var start = articles[i].Index;
            var end = i + 1 < articles.Count ? articles[i + 1].Index : html.Length;
            var block = html[start..end];

            // "Some other movies you might like" lives inside the last article. Cut it off.
            var cut = block.IndexOf("card-scroller", StringComparison.Ordinal);
            if (cut > 0) block = block[..cut];

            var slug = articles[i].Groups["slug"].Value;
            string title = slug, cinema = "", experience = "";

            foreach (Match t in Token().Matches(block))
            {
                if (t.Groups["title"].Success) { title = Decode(t.Groups["title"].Value); continue; }
                if (t.Groups["cinema"].Success) { cinema = Decode(t.Groups["cinema"].Value); experience = ""; continue; }
                if (t.Groups["exp"].Success) { experience = Decode(t.Groups["exp"].Value); continue; }
                if (!t.Groups["id"].Success) continue;

                if (!TryParseTime(Decode(t.Groups["time"].Value), out var time)) continue;

                bool bookable;
                string? bookingUrl = null;

                if (t.Groups["aattrs"].Success)
                {
                    var attrs = t.Groups["aattrs"].Value;
                    var cls = ClassAttr().Match(attrs).Groups["v"].Value;
                    bookable = !cls.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
                    var href = HrefAttr().Match(attrs);
                    if (href.Success) bookingUrl = new Uri(pageUrl, Decode(href.Groups["v"].Value)).ToString();
                }
                else
                {
                    bookable = false; // <span class="action showtime unavailable">
                }

                result.Add(new Showtime(
                    Id: t.Groups["id"].Value,
                    MovieSlug: slug,
                    MovieTitle: title,
                    Cinema: cinema,
                    Experience: string.IsNullOrWhiteSpace(experience) ? "Standard" : experience,
                    Date: date,
                    Time: time,
                    Bookable: bookable,
                    BookingUrl: bookingUrl));
            }
        }

        return result;
    }

    private static (List<CatalogItem> Cinemas, List<CatalogItem> Movies) ParseCatalog(string html)
    {
        var cinemas = new Dictionary<string, CatalogItem>();
        var movies = new Dictionary<string, CatalogItem>();

        foreach (Match m in CatalogInput().Matches(html))
        {
            var item = new CatalogItem(Decode(m.Groups["slug"].Value), Decode(m.Groups["name"].Value).Trim());
            (m.Groups["kind"].Value == "c" ? cinemas : movies).TryAdd(item.Slug, item);
        }

        return ([.. cinemas.Values], [.. movies.Values]);
    }

    private static DateOnly? ReadDateParam(string text)
    {
        var m = DateParam().Match(text);
        return m.Success && DateOnly.TryParseExact(m.Groups["d"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    /// <summary>"Today", "Tomorrow" or "Wed 16 Dec". The year is not shown, so we infer it.</summary>
    internal static DateOnly? ParseTabText(string text, DateOnly today)
    {
        text = text.Trim();
        if (text.Equals("Today", StringComparison.OrdinalIgnoreCase)) return today;
        if (text.Equals("Tomorrow", StringComparison.OrdinalIgnoreCase)) return today.AddDays(1);

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !int.TryParse(parts[^2], out var day)) return null;

        var month = Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames,
            n => n.Length > 0 && parts[^1].StartsWith(n, StringComparison.OrdinalIgnoreCase)) + 1;
        if (month <= 0) return null;

        for (var year = today.Year - 1; year <= today.Year + 1; year++)
        {
            if (day > DateTime.DaysInMonth(year, month)) continue;
            var candidate = new DateOnly(year, month, day);
            if (candidate >= today.AddDays(-2) && candidate <= today.AddDays(330)) return candidate;
        }
        return null;
    }

    internal static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim().ToUpperInvariant(), ["h:mmtt", "hh:mmtt", "H:mm", "HH:mm"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static string Decode(string value) => WebUtility.HtmlDecode(value);
}
