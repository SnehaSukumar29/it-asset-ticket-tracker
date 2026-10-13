namespace AssetTracker.Core.Models;

public sealed class Asset
{
    public required string Tag { get; init; }
    public required AssetType Type { get; init; }
    public required string Model { get; init; }
    public required string SerialNumber { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public DateOnly? WarrantyExpiry { get; init; }
    public AssetStatus Status { get; set; } = AssetStatus.InStock;
    public string? AssignedTo { get; set; }
    public List<HistoryEntry> History { get; init; } = new();
}
