using AssetTracker.Core.Models;
using AssetTracker.Core.Services;

namespace AssetTracker.Cli;

public static class Output
{
    private static string Local(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static void Assets(IReadOnlyList<Asset> assets)
    {
        if (assets.Count == 0) { Console.WriteLine("No assets found."); return; }
        Console.WriteLine($"{"Tag",-8}  {"Type",-13}  {"Model",-24}  {"Status",-9}  {"Assigned to",-12}  Warranty");
        Console.WriteLine(new string('-', 86));
        foreach (var a in assets)
            Console.WriteLine($"{a.Tag,-8}  {a.Type,-13}  {Trim(a.Model, 24),-24}  {a.Status,-9}  {a.AssignedTo ?? "-",-12}  {a.WarrantyExpiry?.ToString("yyyy-MM-dd") ?? "-"}");
    }

    public static void AssetDetail(Asset a)
    {
        Console.WriteLine($"{a.Tag}  {a.Type}  {a.Model}");
        Console.WriteLine($"  Serial:      {a.SerialNumber}");
        Console.WriteLine($"  Status:      {a.Status}");
        Console.WriteLine($"  Assigned to: {a.AssignedTo ?? "-"}");
        Console.WriteLine($"  Purchased:   {a.PurchaseDate:yyyy-MM-dd}");
        Console.WriteLine($"  Warranty:    {a.WarrantyExpiry?.ToString("yyyy-MM-dd") ?? "-"}");
        History(a.History);
    }

    public static void Tickets(IReadOnlyList<Ticket> tickets, TicketService service)
    {
        if (tickets.Count == 0) { Console.WriteLine("No tickets found."); return; }
        Console.WriteLine($"{"ID",-8}  {"Pri",-3}  {"Status",-10}  {"Title",-30}  {"Asset",-8}  {"Analyst",-10}  {"Due",-16}  SLA");
        Console.WriteLine(new string('-', 104));
        foreach (var t in tickets)
        {
            var sla = service.IsBreached(t) ? "BREACHED" : "OK";
            Console.WriteLine($"{t.Id,-8}  {t.Priority,-3}  {t.Status,-10}  {Trim(t.Title, 30),-30}  {t.AssetTag ?? "-",-8}  {t.AssignedTo ?? "-",-10}  {Local(t.DueUtc),-16}  {sla}");
        }
    }

    public static void TicketDetail(Ticket t, TicketService service)
    {
        Console.WriteLine($"{t.Id}  [{t.Priority}]  {t.Title}");
        Console.WriteLine($"  Status:     {t.Status}");
        Console.WriteLine($"  Requester:  {t.Requester}");
        Console.WriteLine($"  Analyst:    {t.AssignedTo ?? "-"}");
        Console.WriteLine($"  Asset:      {t.AssetTag ?? "-"}");
        Console.WriteLine($"  Created:    {Local(t.CreatedUtc)}");
        Console.WriteLine($"  Due:        {Local(t.DueUtc)}  ({(service.IsBreached(t) ? "SLA BREACHED" : "within SLA")})");
        if (t.Description.Length > 0) Console.WriteLine($"  Details:    {t.Description}");
        if (t.Resolution is not null) Console.WriteLine($"  Resolution: {t.Resolution}");
        History(t.History);
    }

    public static void Report(ServiceDeskReport r)
    {
        Console.WriteLine("IT SERVICE DESK REPORT");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("Assets by status:");
        foreach (var (status, count) in r.AssetsByStatus) Console.WriteLine($"  {status,-10} {count,4}");

        Console.WriteLine();
        Console.WriteLine("Open tickets by priority:");
        foreach (var (priority, count) in r.OpenTicketsByPriority)
            Console.WriteLine($"  {$"{priority} ({TicketService.SlaHours[priority]}h SLA)",-13} {count,4}");

        Console.WriteLine();
        Console.WriteLine($"SLA breaches on open tickets: {r.BreachedOpenTickets.Count}");
        foreach (var t in r.BreachedOpenTickets)
            Console.WriteLine($"  {t.Id} [{t.Priority}] {Trim(t.Title, 40)} (was due {Local(t.DueUtc)})");

        Console.WriteLine();
        Console.WriteLine(r.SlaCompliancePercent is null
            ? "SLA compliance (resolved tickets): n/a - nothing resolved yet"
            : $"SLA compliance (resolved tickets): {r.SlaCompliancePercent}% of {r.ResolvedCount}");

        Console.WriteLine();
        Console.WriteLine($"Warranties expiring in the next 90 days: {r.WarrantyExpiringSoon.Count}");
        foreach (var a in r.WarrantyExpiringSoon) Console.WriteLine($"  {a.Tag} {a.Model} - {a.WarrantyExpiry:yyyy-MM-dd}");
        Console.WriteLine($"Warranties already expired (active assets): {r.WarrantyExpired.Count}");
        foreach (var a in r.WarrantyExpired) Console.WriteLine($"  {a.Tag} {a.Model} - {a.WarrantyExpiry:yyyy-MM-dd}");

        if (r.TopProblemAssets.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Assets with the most tickets:");
            foreach (var (tag, count) in r.TopProblemAssets) Console.WriteLine($"  {tag,-8} {count} ticket(s)");
        }
    }

    private static void History(IEnumerable<HistoryEntry> history)
    {
        Console.WriteLine("  History:");
        foreach (var h in history)
            Console.WriteLine($"    {Local(h.TimestampUtc)}  {h.Action,-9}  {h.Detail}");
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "~";
}
