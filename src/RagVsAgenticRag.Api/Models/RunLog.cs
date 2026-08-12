using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RagVsAgenticRag.Api.Models;

public class RunLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string Mode { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required List<RagSource> Sources { get; init; }
    public required List<ToolTraceEntry> Trace { get; init; }
    public required RagMetrics Metrics { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
