namespace AssetTracker.Core.Models;

/// <summary>Everything that gets persisted to disk.</summary>
public sealed class TrackerData
{
    public int SchemaVersion { get; set; } = 1;
    public int NextTicketNumber { get; set; } = 1;
    public List<Asset> Assets { get; init; } = new();
    public List<Ticket> Tickets { get; init; } = new();
}
