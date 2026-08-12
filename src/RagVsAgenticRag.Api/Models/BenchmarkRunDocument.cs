using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RagVsAgenticRag.Api.Models;

public class BenchmarkRunDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public DateTime RanAt { get; init; } = DateTime.UtcNow;
    public required string ChatModel { get; init; }
    public required List<BenchmarkEntry> Entries { get; init; }
}
