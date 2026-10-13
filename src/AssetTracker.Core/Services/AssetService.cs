using AssetTracker.Core.Models;

namespace AssetTracker.Core.Services;

public sealed class AssetService(TrackerData data, IClock clock)
{
    public Asset Add(string tag, string type, string model, string serial, string purchaseDate, string? warrantyExpiry)
    {
        var asset = new Asset
        {
            Tag = Validate.AssetTag(tag),
            Type = Validate.Enum<AssetType>(type, "Asset type"),
            Model = Validate.Text(model, "Model", 80),
            SerialNumber = Validate.Serial(serial),
            PurchaseDate = Validate.Date(purchaseDate, "Purchase date"),
            WarrantyExpiry = string.IsNullOrWhiteSpace(warrantyExpiry) ? null : Validate.Date(warrantyExpiry, "Warranty expiry"),
        };

        if (asset.PurchaseDate > DateOnly.FromDateTime(clock.UtcNow))
            throw new TrackerException("Purchase date cannot be in the future.");
        if (asset.WarrantyExpiry is { } w && w < asset.PurchaseDate)
            throw new TrackerException("Warranty expiry cannot be before the purchase date.");
        if (data.Assets.Any(a => a.Tag == asset.Tag))
            throw new TrackerException($"Asset {asset.Tag} already exists.");
        if (data.Assets.Any(a => a.SerialNumber == asset.SerialNumber))
            throw new TrackerException($"Serial number {asset.SerialNumber} is already registered.");

        Record(asset, "Created", $"{asset.Type} {asset.Model} added to stock");
        data.Assets.Add(asset);
        return asset;
    }

    public Asset Get(string tag)
    {
        var t = Validate.AssetTag(tag);
        return data.Assets.FirstOrDefault(a => a.Tag == t)
            ?? throw new TrackerException($"Asset {t} not found.");
    }

    public IReadOnlyList<Asset> List(string? status = null, string? assignedTo = null)
    {
        IEnumerable<Asset> q = data.Assets;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = Validate.Enum<AssetStatus>(status, "Status");
            q = q.Where(a => a.Status == s);
        }
        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            var u = Validate.Username(assignedTo);
            q = q.Where(a => a.AssignedTo == u);
        }
        return q.OrderBy(a => a.Tag, StringComparer.Ordinal).ToList();
    }

    public Asset Assign(string tag, string username)
    {
        var asset = Get(tag);
        var user = Validate.Username(username);
        if (asset.Status == AssetStatus.Retired)
            throw new TrackerException($"{asset.Tag} is retired and cannot be assigned.");
        if (asset.Status == AssetStatus.InRepair)
            throw new TrackerException($"{asset.Tag} is in repair. Return it to stock first.");
        if (asset.AssignedTo == user)
            throw new TrackerException($"{asset.Tag} is already assigned to {user}.");

        var previous = asset.AssignedTo;
        asset.AssignedTo = user;
        asset.Status = AssetStatus.Assigned;
        Record(asset, "Assigned", previous is null ? $"Assigned to {user}" : $"Reassigned from {previous} to {user}");
        return asset;
    }

    /// <summary>Return an asset to stock (e.g. leaver offboarding) and clear its assignment.</summary>
    public Asset ReturnToStock(string tag)
    {
        var asset = Get(tag);
        if (asset.Status == AssetStatus.Retired)
            throw new TrackerException($"{asset.Tag} is retired.");
        if (asset.Status == AssetStatus.InStock)
            throw new TrackerException($"{asset.Tag} is already in stock.");
        var previous = asset.AssignedTo;
        asset.AssignedTo = null;
        asset.Status = AssetStatus.InStock;
        Record(asset, "Returned", previous is null ? "Returned to stock" : $"Returned to stock from {previous}");
        return asset;
    }

    public Asset SendForRepair(string tag, string reason)
    {
        var asset = Get(tag);
        var why = Validate.Text(reason, "Repair reason", 200);
        if (asset.Status == AssetStatus.Retired)
            throw new TrackerException($"{asset.Tag} is retired.");
        if (asset.Status == AssetStatus.InRepair)
            throw new TrackerException($"{asset.Tag} is already in repair.");
        var previous = asset.AssignedTo;
        asset.AssignedTo = null;
        asset.Status = AssetStatus.InRepair;
        Record(asset, "Repair", previous is null ? why : $"{why} (unassigned from {previous})");
        return asset;
    }

    public Asset Retire(string tag, string reason)
    {
        var asset = Get(tag);
        var why = Validate.Text(reason, "Retirement reason", 200);
        if (asset.Status == AssetStatus.Retired)
            throw new TrackerException($"{asset.Tag} is already retired.");
        var open = data.Tickets.Where(t => t.AssetTag == asset.Tag && t.IsOpen).Select(t => t.Id).ToList();
        if (open.Count > 0)
            throw new TrackerException($"{asset.Tag} has open tickets ({string.Join(", ", open)}). Resolve them before retiring.");
        asset.AssignedTo = null;
        asset.Status = AssetStatus.Retired;
        Record(asset, "Retired", why);
        return asset;
    }

    private void Record(Asset asset, string action, string detail) =>
        asset.History.Add(new HistoryEntry(clock.UtcNow, action, detail));
}
