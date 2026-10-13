using AssetTracker.Core.Models;

namespace AssetTracker.Core.Services;

public sealed record ServiceDeskReport(
    IReadOnlyDictionary<AssetStatus, int> AssetsByStatus,
    IReadOnlyList<Asset> WarrantyExpiringSoon,
    IReadOnlyList<Asset> WarrantyExpired,
    IReadOnlyDictionary<TicketPriority, int> OpenTicketsByPriority,
    IReadOnlyList<Ticket> BreachedOpenTickets,
    int ResolvedCount,
    double? SlaCompliancePercent,
    IReadOnlyList<(string AssetTag, int TicketCount)> TopProblemAssets);

public sealed class ReportService(TrackerData data, IClock clock)
{
    public ServiceDeskReport Build(int warrantyWindowDays = 90)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var tickets = new TicketService(data, clock);
        var active = data.Assets.Where(a => a.Status != AssetStatus.Retired).ToList();

        var byStatus = Enum.GetValues<AssetStatus>()
            .ToDictionary(s => s, s => data.Assets.Count(a => a.Status == s));

        var expiringSoon = active
            .Where(a => a.WarrantyExpiry is { } w && w >= today && w <= today.AddDays(warrantyWindowDays))
            .OrderBy(a => a.WarrantyExpiry).ToList();
        var expired = active
            .Where(a => a.WarrantyExpiry is { } w && w < today)
            .OrderBy(a => a.WarrantyExpiry).ToList();

        var open = data.Tickets.Where(t => t.IsOpen).ToList();
        var openByPriority = Enum.GetValues<TicketPriority>()
            .ToDictionary(p => p, p => open.Count(t => t.Priority == p));
        var breached = open.Where(tickets.IsBreached).OrderBy(t => t.DueUtc).ToList();

        var resolved = data.Tickets.Where(t => t.ResolvedUtc is not null).ToList();
        double? compliance = resolved.Count == 0
            ? null
            : Math.Round(100.0 * resolved.Count(t => t.ResolvedUtc <= t.DueUtc) / resolved.Count, 1);

        var topAssets = data.Tickets
            .Where(t => t.AssetTag is not null)
            .GroupBy(t => t.AssetTag!)
            .Select(g => (AssetTag: g.Key, TicketCount: g.Count()))
            .OrderByDescending(x => x.TicketCount).ThenBy(x => x.AssetTag, StringComparer.Ordinal)
            .Take(5).ToList();

        return new ServiceDeskReport(byStatus, expiringSoon, expired, openByPriority, breached,
            resolved.Count, compliance, topAssets);
    }
}
