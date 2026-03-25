using System.Text.Json;

namespace Dxs.Consigliere.Benchmarks.Replay;

public static class ReplayScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ReplayScenario Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Replay fixture not found: {path}", path);

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<ReplayScenarioDto>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize replay fixture: {path}");

        return new ReplayScenario(
            dto.Name ?? "Unnamed replay scenario",
            (dto.Observations ?? [])
                .Select(o => new ReplayObservation(
                    o.Sequence,
                    Enum.Parse<ReplayEventType>(o.EventType, ignoreCase: true),
                    o.Source ?? "unknown",
                    o.EntityId ?? string.Empty,
                    o.BlockHash,
                    o.BlockHeight,
                    o.ObservedAtUtc,
                    o.PayloadRef))
                .OrderBy(x => x.Sequence)
                .ToList());
    }

    private sealed class ReplayScenarioDto
    {
        public string? Name { get; set; }
        public List<ReplayObservationDto>? Observations { get; set; }
    }

    private sealed class ReplayObservationDto
    {
        public long Sequence { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? EntityId { get; set; }
        public string? BlockHash { get; set; }
        public int? BlockHeight { get; set; }
        public DateTimeOffset ObservedAtUtc { get; set; }
        public string? PayloadRef { get; set; }
    }
}
