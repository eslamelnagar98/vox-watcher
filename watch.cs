// VOX Egypt booking watcher
// Run: dotnet run watch.cs   (.NET 10 file-based app, no project file needed)
// Env: NTFY_TOPIC = your private ntfy topic name

using System.Text.RegularExpressions;

const string Url = "https://egy.voxcinemas.com/showtimes";
const string StateFile = "state/dates.txt";
const string LogFile = "state/open-log.txt";

var topic = Environment.GetEnvironmentVariable("NTFY_TOPIC");
if (string.IsNullOrWhiteSpace(topic))
{
    Console.Error.WriteLine("NTFY_TOPIC is missing.");
    return 2;
}

var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
var nowCairo = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, cairo);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");
http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

var html = await http.GetStringAsync(Url);

// The date tabs on the page link to showtimes?d=YYYYMMDD
var current = Regex.Matches(html, @"[?&](?:amp;)?d=(\d{8})")
    .Select(m => m.Groups[1].Value)
    .ToHashSet();

if (current.Count == 0)
{
    await Notify(topic, "VOX watcher problem",
        "No dates found on the page. The site layout may have changed.", "warning", 4, Url);
    return 1;
}

var previous = File.Exists(StateFile)
    ? File.ReadAllLines(StateFile).Where(l => l.Length == 8).ToHashSet()
    : new HashSet<string>();

var added = current.Except(previous).Order().ToList();
var firstRun = previous.Count == 0;

Console.WriteLine($"{nowCairo:yyyy-MM-dd HH:mm} Cairo | dates on site: {string.Join(",", current.Order())}");

if (!firstRun && added.Count > 0)
{
    var pretty = string.Join(", ", added.Select(d =>
        DateTime.ParseExact(d, "yyyyMMdd", null).ToString("ddd dd MMM")));

    await Notify(topic, "VOX booking is OPEN",
        $"New dates: {pretty}", "rotating_light", 5, $"{Url}?d={added[0]}");

    File.AppendAllText(LogFile, $"{nowCairo:yyyy-MM-dd ddd HH:mm} Cairo | {string.Join(",", added)}{Environment.NewLine}");
    Console.WriteLine($"NEW: {pretty}");
}

Directory.CreateDirectory("state");
File.WriteAllLines(StateFile, current.Order());
return 0;

static async Task Notify(string topic, string title, string message, string tag, int priority, string clickUrl)
{
    using var client = new HttpClient();
    using var req = new HttpRequestMessage(HttpMethod.Post, $"https://ntfy.sh/{topic}")
    {
        Content = new StringContent(message)
    };
    req.Headers.Add("Title", title);
    req.Headers.Add("Priority", priority.ToString());
    req.Headers.Add("Tags", tag);
    req.Headers.Add("Click", clickUrl);
    var res = await client.SendAsync(req);
    Console.WriteLine($"ntfy: {(int)res.StatusCode}");
}
