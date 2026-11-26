namespace VoxWatcher.Domain;

/// <summary>One screening as shown on the VOX showtimes page.</summary>
public sealed record Showtime(
    string Id,
    string MovieSlug,
    string MovieTitle,
    string Cinema,
    string Experience,
    DateOnly Date,
    TimeOnly Time,
    bool Bookable,
    string? BookingUrl);

/// <summary>A cinema or movie from the site's filter dropdowns.</summary>
public sealed record CatalogItem(string Slug, string Name);

/// <summary>Everything we read from one showtimes page.</summary>
public sealed record ShowtimesPage(
    DateOnly? PageDate,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<Showtime> Showtimes,
    IReadOnlyList<CatalogItem> Cinemas,
    IReadOnlyList<CatalogItem> Movies,
    bool HasNoShowtimesMessage)
{
    /// <summary>True when the page looks like a real VOX showtimes page.</summary>
    public bool LooksValid => Cinemas.Count > 0 || Movies.Count > 0 || Dates.Count > 0 || HasNoShowtimesMessage;
}

public enum ChangeKind
{
    /// <summary>A new date appeared for this watch.</summary>
    DateAdded,
    /// <summary>A new showtime is listed but not bookable yet.</summary>
    SessionListed,
    /// <summary>A showtime became bookable for the first time.</summary>
    BookingOpened,
    /// <summary>A showtime that sold out is bookable again (seats released).</summary>
    SeatsReturned,
    /// <summary>A bookable showtime became unavailable (sold out or closed).</summary>
    SoldOut,
    /// <summary>A watched movie appeared on the site for the first time.</summary>
    MovieListed
}

public sealed record Change(ChangeKind Kind, DateOnly? Date, Showtime? Showtime = null, string? Note = null);
