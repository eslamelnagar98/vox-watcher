using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoxWatcher.Domain;

namespace VoxWatcher.Configuration;

public sealed class WatchConfig
{
    /// <summary>VOX country site. Egypt by default.</summary>
    public string Site { get; init; } = "https://egy.voxcinemas.com";

    /// <summary>IANA time zone used for dates, logs and insights.</summary>
    public string TimeZone { get; init; } = "Africa/Cairo";

    /// <summary>Push a message when ANY new movie appears on the site.</summary>
    public bool AnnounceNewMovies { get; init; }

    public List<WatchDefinition> Watches { get; init; } = [];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static WatchConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new ConfigException($"Config file not found: {path}");

        WatchConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<WatchConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"watches.json is not valid JSON: {ex.Message}");
        }

        if (config is null)
            throw new ConfigException("watches.json is empty.");

        config.Validate();
        return config;
    }

    private void Validate()
    {
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var w in Watches)
        {
            if (string.IsNullOrWhiteSpace(w.Name)) errors.Add("Every watch needs a \"name\".");
            else if (!names.Add(w.Name)) errors.Add($"Duplicate watch name: {w.Name}");

            if (w.Dates is { } d)
            {
                if (d.From is { } f && d.To is { } t && f > t)
                    errors.Add($"[{w.Name}] dates.from is after dates.to.");
            }

            foreach (var day in w.DaysOfWeek)
                if (!TryParseDay(day, out _)) errors.Add($"[{w.Name}] Unknown day \"{day}\". Use Sat, Sun, Mon, Tue, Wed, Thu, Fri.");

            if (w.Time?.After is { } a && !TimeOnly.TryParse(a, CultureInfo.InvariantCulture, out _))
                errors.Add($"[{w.Name}] time.after \"{a}\" is not a time like 18:00.");
            if (w.Time?.Before is { } b && !TimeOnly.TryParse(b, CultureInfo.InvariantCulture, out _))
                errors.Add($"[{w.Name}] time.before \"{b}\" is not a time like 23:30.");
        }

        if (errors.Count > 0)
            throw new ConfigException("Config problems:\n  " + string.Join("\n  ", errors));
    }

    internal static bool TryParseDay(string value, out DayOfWeek day)
    {
        foreach (var candidate in Enum.GetValues<DayOfWeek>())
        {
            if (candidate.ToString().StartsWith(value.Trim(), StringComparison.OrdinalIgnoreCase) && value.Trim().Length >= 3)
            {
                day = candidate;
                return true;
            }
        }
        day = default;
        return false;
    }
}

public sealed class WatchDefinition
{
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;

    /// <summary>Cinema slugs. Empty = all cinemas.</summary>
    public List<string> Cinemas { get; init; } = [];

    /// <summary>Movie slugs. Empty = all movies.</summary>
    public List<string> Movies { get; init; } = [];

    public DateFilter? Dates { get; init; }

    /// <summary>Sat, Sun, Mon ... Empty = any day.</summary>
    public List<string> DaysOfWeek { get; init; } = [];

    /// <summary>Experience names as shown on the site (GOLD, IMAX, 4DX, MAX, Standard ...). Empty = any.</summary>
    public List<string> Experiences { get; init; } = [];

    public TimeFilter? Time { get; init; }

    public AlertSettings Alerts { get; init; } = new();

    [JsonIgnore]
    public string Key => Slugify(Name);

    public bool MatchesDate(DateOnly date)
    {
        if (Dates is { } d)
        {
            if (d.From is { } from && date < from) return false;
            if (d.To is { } to && date > to) return false;
            if (d.On is { Count: > 0 } on && !on.Contains(date)) return false;
        }

        if (DaysOfWeek.Count > 0)
        {
            var allowed = DaysOfWeek.Select(x => WatchConfig.TryParseDay(x, out var dw) ? dw : (DayOfWeek?)null);
            if (!allowed.Contains(date.DayOfWeek)) return false;
        }

        return true;
    }

    public bool Matches(Showtime s)
    {
        if (!MatchesDate(s.Date)) return false;

        if (Experiences.Count > 0 &&
            !Experiences.Any(e => s.Experience.Contains(e.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;

        if (Time?.After is { } a && s.Time < TimeOnly.Parse(a, CultureInfo.InvariantCulture)) return false;
        if (Time?.Before is { } b && s.Time > TimeOnly.Parse(b, CultureInfo.InvariantCulture)) return false;

        return true;
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed class DateFilter
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public List<DateOnly>? On { get; init; }
}

public sealed class TimeFilter
{
    /// <summary>Earliest start time, 24h, e.g. "18:00".</summary>
    public string? After { get; init; }

    /// <summary>Latest start time, 24h, e.g. "23:30".</summary>
    public string? Before { get; init; }
}

public sealed class AlertSettings
{
    public bool BookingOpened { get; init; } = true;
    public bool SeatsReturned { get; init; } = true;
    public bool NewShowtimes { get; init; } = true;
    public bool SoldOut { get; init; }
}

public sealed class ConfigException(string message) : Exception(message);
