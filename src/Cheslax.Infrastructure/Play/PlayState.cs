using System.Text.Json;
using Cheslax.GameEngine.OAnQuan;

namespace Cheslax.Infrastructure.Play;

internal sealed class PersistedPlayState
{
    public string? ChessFen { get; set; }
    public List<string> ChessHistory { get; set; } = [];
    public string? LastFrom { get; set; }
    public string? LastTo { get; set; }
    public int? WhiteMs { get; set; }
    public int? BlackMs { get; set; }
    public string? ClockRunning { get; set; }
    public DateTimeOffset? ClockUpdatedAt { get; set; }
    public OAnQuanState? OAnQuan { get; set; }
    public List<string> OAnQuanHistory { get; set; } = [];
    public int? LastPit { get; set; }
    public string? LastDirection { get; set; }
    public List<Dictionary<string, object?>> LastEvents { get; set; } = [];
}

internal static class PlayJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(PersistedPlayState state) => JsonSerializer.Serialize(state, Options);

    public static PersistedPlayState Parse(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new PersistedPlayState()
            : JsonSerializer.Deserialize<PersistedPlayState>(json, Options) ?? new PersistedPlayState();
}

internal static class OAnQuanPublicMapper
{
    public static object ToPublicState(OAnQuanState state) => new
    {
        pits = state.Pits.Select(pit => new
        {
            index = pit.Index,
            type = pit.IsQuan ? "quan" : "dan",
            dan = pit.Dan,
            quan = pit.Quan,
            owner = pit.Owner,
        }),
        currentPlayer = state.CurrentPlayer,
        captured = new[]
        {
            new { dan = state.Captured[0].Dan, quan = state.Captured[0].Quan },
            new { dan = state.Captured[1].Dan, quan = state.Captured[1].Quan },
        },
        status = state.Status,
        winner = state.Winner is "0" or "1" ? int.Parse(state.Winner) : (object?)state.Winner,
        endReason = state.EndReason switch
        {
            OAnQuanEndReason.BothQuanEmpty => "both_quan_empty",
            OAnQuanEndReason.CannotResupply => "cannot_resupply",
            _ => (string?)null,
        },
        resupplied = state.Resupplied,
    };

    public static List<Dictionary<string, object?>> ToPublicEvents(IEnumerable<OAnQuanEvent> events)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var item in events)
        {
            list.Add(item switch
            {
                PickEvent pick => new() { ["type"] = "pick", ["pit"] = pick.Pit, ["dan"] = pick.Dan },
                DropEvent drop => new() { ["type"] = "drop", ["pit"] = drop.Pit },
                RelayEvent relay => new() { ["type"] = "relay", ["pit"] = relay.Pit, ["dan"] = relay.Dan },
                CaptureEvent capture => new() { ["type"] = "capture", ["pit"] = capture.Pit, ["dan"] = capture.Dan, ["quan"] = capture.Quan },
                ResupplyEvent resupply => new() { ["type"] = "resupply", ["player"] = resupply.Player, ["convertedQuan"] = resupply.ConvertedQuan },
                TurnEndEvent turn => new() { ["type"] = "turnEnd", ["nextPlayer"] = turn.NextPlayer },
                GameEndEvent end => new()
                {
                    ["type"] = "gameEnd",
                    ["winner"] = end.Winner is "0" or "1" ? int.Parse(end.Winner) : end.Winner,
                    ["reason"] = end.Reason == OAnQuanEndReason.CannotResupply ? "cannot_resupply" : "both_quan_empty",
                },
                _ => new() { ["type"] = "unknown" },
            });
        }

        return list;
    }
}
