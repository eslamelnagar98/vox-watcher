using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace VoxWatcher.Notifications;

public sealed record NotificationAction(string Label, string Url);

public sealed record Notification(
    string Title,
    string Message,
    int Priority,
    IReadOnlyList<string> Tags,
    string? Click = null,
    IReadOnlyList<NotificationAction>? Actions = null);

public interface INotifier
{
    Task SendAsync(Notification n, CancellationToken ct);
}

/// <summary>Prints instead of sending. Used for dry runs and local testing.</summary>
public sealed class ConsoleNotifier : INotifier
{
    public List<Notification> Sent { get; } = [];

    public Task SendAsync(Notification n, CancellationToken ct)
    {
        Sent.Add(n);
        Console.WriteLine();
        Console.WriteLine($"  [PUSH p{n.Priority}] {n.Title}");
        foreach (var line in n.Message.Split('\n')) Console.WriteLine($"    {line}");
        if (n.Click is not null) Console.WriteLine($"    click: {n.Click}");
        foreach (var a in n.Actions ?? []) Console.WriteLine($"    button: {a.Label} -> {a.Url}");
        return Task.CompletedTask;
    }
}

/// <summary>Publishes to ntfy (https://ntfy.sh or your own server) using the JSON API, so emojis and Arabic work.</summary>
public sealed class NtfyNotifier(string server, string topic, string? token) : INotifier
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    public async Task SendAsync(Notification n, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await PostAsync(n, ct);
            Console.WriteLine($"  ntfy -> {(int)response.StatusCode} ({n.Title})");
            var retryable = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
            if (!retryable || attempt >= RetryDelays.Length)
            {
                response.EnsureSuccessStatusCode();
                return;
            }

            var wait = response.Headers.RetryAfter?.Delta ?? RetryDelays[attempt];
            await Task.Delay(wait > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : wait, ct);
        }
    }

    private Task<HttpResponseMessage> PostAsync(Notification n, CancellationToken ct)
    {
        var payload = new NtfyMessage(
            Topic: topic,
            Title: n.Title,
            Message: n.Message,
            Priority: Math.Clamp(n.Priority, 1, 5),
            Tags: n.Tags,
            Click: n.Click,
            Actions: n.Actions?.Take(3).Select(a => new NtfyAction("view", a.Label, a.Url, true)).ToList());

        var request = new HttpRequestMessage(HttpMethod.Post, server.TrimEnd('/'))
        {
            Content = JsonContent.Create(payload)
        };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new("Bearer", token);

        return Client.SendAsync(request, ct);
    }

    private sealed record NtfyMessage(
        [property: JsonPropertyName("topic")] string Topic,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("priority")] int Priority,
        [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
        [property: JsonPropertyName("click"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Click,
        [property: JsonPropertyName("actions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<NtfyAction>? Actions);

    private sealed record NtfyAction(
        [property: JsonPropertyName("action")] string Action,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("clear")] bool Clear);
}
