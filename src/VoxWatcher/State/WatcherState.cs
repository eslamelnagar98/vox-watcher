using System.Text.Json;
using VoxWatcher.Configuration;

namespace VoxWatcher.State;

/// <summary>Everything the watcher remembers between runs. Saved in state/state.json.</summary>
public sealed class WatcherState
{
    public Dictionary<string, WatchState> Watches { get; set; } = [];

    /// <summary>Movie slugs seen on the site. Used to detect new movies.</summary>
    public SortedSet<string> KnownMovies { get; set; } = [];

    public HealthState Health { get; set; } = new();

    /// <summary>Each run where booking opened. Used to learn the opening pattern.</summary>
    public List<OpeningRecord> Openings { get; set; } = [];

    /// <summary>How long sessions stayed bookable before selling out.</summary>
    public List<SellOutRecord> SellOuts { get; set; } = [];
}

public sealed class WatchState
{
    public SortedSet<DateOnly> Dates { get; set; } = [];
    public SortedDictionary<string, SessionState> Sessions { get; set; } = [];
}

public sealed class SessionState
{
    public string Movie { get; set; } = "";
    public string Cinema { get; set; } = "";
    public string Experience { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public bool Bookable { get; set; }
    public bool EverBookable { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? SoldOutAt { get; set; }
}

public sealed class HealthState
{
    public int ConsecutiveFailures { get; set; }
    public bool Alerted { get; set; }
    public string? LastError { get; set; }
}

public sealed record OpeningRecord(DateTimeOffset At, string Watch, string Movie, int Sessions, List<DateOnly> Dates);

public sealed record SellOutRecord(string Movie, string Cinema, string Experience, DateOnly Date, double MinutesBookable);

public sealed class StateStore(string directory)
{
    private string StatePath => Path.Combine(directory, "state.json");
    public string EventsLogPath => Path.Combine(directory, "events.log");

    public WatcherState Load()
    {
        if (!File.Exists(StatePath)) return new WatcherState();
        try
        {
            return JsonSerializer.Deserialize<WatcherState>(File.ReadAllText(StatePath), WatchConfig.JsonOptions) ?? new WatcherState();
        }
        catch (JsonException)
        {
            // A broken state file should never stop the watcher. Start fresh (a silent baseline run).
            return new WatcherState();
        }
    }

    /// <summary>Writes only when content changed, so git commits only real changes.</summary>
    public void Save(WatcherState state)
    {
        Directory.CreateDirectory(directory);
        WriteIfChanged(StatePath, JsonSerializer.Serialize(state, WatchConfig.JsonOptions));
    }

    public void AppendEvents(IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) return;
        Directory.CreateDirectory(directory);
        File.AppendAllLines(EventsLogPath, list);
    }

    public void WriteText(string fileName, string content)
    {
        Directory.CreateDirectory(directory);
        WriteIfChanged(Path.Combine(directory, fileName), content);
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content) return;
        File.WriteAllText(path, content);
    }
}
