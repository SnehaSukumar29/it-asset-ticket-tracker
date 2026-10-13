using System.Text.Json.Serialization;

namespace AssetTracker.Core.Models;

public sealed class Ticket
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public required string Requester { get; init; }
    public required TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public string? AssetTag { get; init; }
    public string? AssignedTo { get; set; }
    public required DateTime CreatedUtc { get; init; }
    public required DateTime DueUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
    public string? Resolution { get; set; }
    public List<HistoryEntry> History { get; init; } = new();

    [JsonIgnore]
    public bool IsOpen => Status is TicketStatus.New or TicketStatus.InProgress;
}
