using AssetTracker.Cli;
using AssetTracker.Core.Services;
using AssetTracker.Core.Storage;

const string Usage = """
IT Asset & Ticket Tracker

Usage: assettracker [--data FILE] <command> [options]

Assets
  asset add --tag LT-0001 --type Laptop --model "Dell Latitude 5440" --serial ABC123
            --purchased 2025-01-15 [--warranty 2028-01-15]
  asset list [--status InStock|Assigned|InRepair|Retired] [--user s.rose]
  asset show LT-0001
  asset assign LT-0001 --to s.rose
  asset return LT-0001                       (back to stock, e.g. leaver)
  asset repair LT-0001 --reason "Cracked screen"
  asset retire LT-0001 --reason "End of life"

Tickets
  ticket new --title "VPN not connecting" --requester j.hopper --priority P2
             [--asset LT-0002] [--desc "More detail"]
  ticket list [--open] [--priority P1-P4] [--analyst it.support]
  ticket show INC-0001
  ticket assign INC-0001 --to it.support
  ticket note INC-0001 --text "Checked event logs"
  ticket priority INC-0001 P1 --reason "Affects whole finance team"
  ticket status INC-0001 InProgress|Resolved|Closed [--resolution "What fixed it"]

Other
  report                                      service desk dashboard
  demo                                        load fictional demo data (empty file only)

Data is saved to --data FILE, $ASSET_TRACKER_DATA, or a per-user app data folder.
""";

var argList = args.ToList();
string? dataPath = Environment.GetEnvironmentVariable("ASSET_TRACKER_DATA");
var dataIndex = argList.IndexOf("--data");
if (dataIndex >= 0)
{
    if (dataIndex + 1 >= argList.Count) { Console.Error.WriteLine("Error: --data needs a file path."); return 2; }
    dataPath = argList[dataIndex + 1];
    argList.RemoveRange(dataIndex, 2);
}

if (argList.Count == 0 || argList[0] is "help" or "--help" or "-h")
{
    Console.WriteLine(Usage);
    return argList.Count == 0 ? 2 : 0;
}

var store = new JsonDataStore(string.IsNullOrWhiteSpace(dataPath) ? JsonDataStore.DefaultPath() : dataPath);
var clock = new SystemClock();

try
{
    var data = store.Load();
    var assets = new AssetService(data, clock);
    var tickets = new TicketService(data, clock);
    var cmd = new CommandArgs(argList.Skip(2));
    var group = argList[0].ToLowerInvariant();
    var action = argList.Count > 1 ? argList[1].ToLowerInvariant() : "";
    var changed = true;

    switch (group, action)
    {
        case ("asset", "add"):
            var created = assets.Add(cmd.Require("tag"), cmd.Require("type"), cmd.Require("model"),
                cmd.Require("serial"), cmd.Require("purchased"), cmd.Get("warranty"));
            Console.WriteLine($"Added {created.Tag} ({created.Type} {created.Model}).");
            break;
        case ("asset", "list"):
            Output.Assets(assets.List(cmd.Get("status"), cmd.Get("user")));
            changed = false;
            break;
        case ("asset", "show"):
            Output.AssetDetail(assets.Get(cmd.Positional(0, "asset tag")));
            changed = false;
            break;
        case ("asset", "assign"):
            var assigned = assets.Assign(cmd.Positional(0, "asset tag"), cmd.Require("to"));
            Console.WriteLine($"{assigned.Tag} assigned to {assigned.AssignedTo}.");
            break;
        case ("asset", "return"):
            Console.WriteLine($"{assets.ReturnToStock(cmd.Positional(0, "asset tag")).Tag} returned to stock.");
            break;
        case ("asset", "repair"):
            Console.WriteLine($"{assets.SendForRepair(cmd.Positional(0, "asset tag"), cmd.Require("reason")).Tag} marked as in repair.");
            break;
        case ("asset", "retire"):
            Console.WriteLine($"{assets.Retire(cmd.Positional(0, "asset tag"), cmd.Require("reason")).Tag} retired.");
            break;

        case ("ticket", "new"):
            var t = tickets.Create(cmd.Require("title"), cmd.Require("requester"), cmd.Require("priority"),
                cmd.Get("desc"), cmd.Get("asset"));
            Console.WriteLine($"Logged {t.Id} [{t.Priority}] - SLA due {t.DueUtc.ToLocalTime():yyyy-MM-dd HH:mm}.");
            break;
        case ("ticket", "list"):
            Output.Tickets(tickets.List(cmd.Has("open"), cmd.Get("priority"), cmd.Get("analyst")), tickets);
            changed = false;
            break;
        case ("ticket", "show"):
            Output.TicketDetail(tickets.Get(cmd.Positional(0, "ticket ID")), tickets);
            changed = false;
            break;
        case ("ticket", "assign"):
            var ta = tickets.Assign(cmd.Positional(0, "ticket ID"), cmd.Require("to"));
            Console.WriteLine($"{ta.Id} assigned to {ta.AssignedTo}.");
            break;
        case ("ticket", "note"):
            Console.WriteLine($"Note added to {tickets.AddNote(cmd.Positional(0, "ticket ID"), cmd.Require("text")).Id}.");
            break;
        case ("ticket", "priority"):
            var tp = tickets.ChangePriority(cmd.Positional(0, "ticket ID"), cmd.Positional(1, "new priority"), cmd.Require("reason"));
            Console.WriteLine($"{tp.Id} is now {tp.Priority}; SLA due {tp.DueUtc.ToLocalTime():yyyy-MM-dd HH:mm}.");
            break;
        case ("ticket", "status"):
            var ts = tickets.ChangeStatus(cmd.Positional(0, "ticket ID"), cmd.Positional(1, "new status"), cmd.Get("resolution"));
            Console.WriteLine($"{ts.Id} is now {ts.Status}.");
            break;

        case ("report", _):
            Output.Report(new ReportService(data, clock).Build());
            changed = false;
            break;
        case ("demo", _):
            DemoData.Load(data, clock);
            Console.WriteLine($"Loaded demo data: {data.Assets.Count} assets, {data.Tickets.Count} tickets.");
            break;

        default:
            Console.Error.WriteLine($"Unknown command '{string.Join(' ', argList.Take(2))}'. Run 'assettracker help'.");
            return 2;
    }

    if (changed) store.Save(data);
    return 0;
}
catch (TrackerException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
{
    Console.Error.WriteLine($"Data file error: {ex.Message}");
    return 1;
}
