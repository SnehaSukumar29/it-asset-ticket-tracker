using AssetTracker.Core.Models;
using AssetTracker.Core.Services;

namespace AssetTracker.Cli;

/// <summary>Fictional Shiningstar Logistics data for demonstrating the tool.</summary>
public static class DemoData
{
    public static void Load(TrackerData data, IClock clock)
    {
        if (data.Assets.Count > 0 || data.Tickets.Count > 0)
            throw new TrackerException("Demo data can only be loaded into an empty data file. Use --data to point at a new file.");

        var today = DateOnly.FromDateTime(clock.UtcNow);
        string D(int daysFromToday) => today.AddDays(daysFromToday).ToString("yyyy-MM-dd");

        var assets = new AssetService(data, clock);
        assets.Add("LT-0001", "Laptop", "Dell Latitude 5440", "DL5440A91", D(-700), D(395));
        assets.Add("LT-0002", "Laptop", "Dell Latitude 5440", "DL5440B27", D(-700), D(395));
        assets.Add("LT-0003", "Laptop", "Lenovo ThinkPad T14", "LNT14C882", D(-1050), D(45));
        assets.Add("DT-0001", "Desktop", "HP EliteDesk 800 G6", "HPED6X410", D(-1300), D(-200));
        assets.Add("MN-0001", "Monitor", "Dell P2422H", "DP2422H07", D(-400), D(700));
        assets.Add("PH-0001", "Phone", "iPhone 13", "IPH13Q551", D(-600), D(20));
        assets.Add("PR-0001", "Printer", "HP LaserJet M404", "HPLJ404Z3", D(-900), D(-100));

        assets.Assign("LT-0001", "s.rose");
        assets.Assign("LT-0002", "j.hopper");
        assets.Assign("LT-0003", "s.jones");
        assets.Assign("PH-0001", "j.hopper");
        assets.SendForRepair("DT-0001", "Fails to POST - suspected PSU fault");

        var tickets = new TicketService(data, clock);
        var t1 = tickets.Create("Cannot connect to VPN from home", "j.hopper", "P2", "Error 809 since this morning", "LT-0002");
        tickets.Assign(t1.Id, "it.support");
        tickets.ChangeStatus(t1.Id, "InProgress");

        var t2 = tickets.Create("Printer jamming on every job", "s.jones", "P3", null, "PR-0001");
        tickets.Assign(t2.Id, "it.support");

        tickets.Create("Request: second monitor for finance desk", "j.hopper", "P4");

        var t4 = tickets.Create("Outlook keeps asking for password", "s.rose", "P3", null, "LT-0001");
        tickets.Assign(t4.Id, "it.support");
        tickets.ChangeStatus(t4.Id, "InProgress");
        tickets.AddNote(t4.Id, "Cleared cached credentials in Credential Manager");
        tickets.ChangeStatus(t4.Id, "Resolved", "Stale cached credentials removed; user re-authenticated with MFA");
        tickets.ChangeStatus(t4.Id, "Closed");
    }
}
