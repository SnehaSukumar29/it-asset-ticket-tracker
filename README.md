# IT Asset & Ticket Tracker (C# / .NET 8)

![tests](https://github.com/SnehaSukumar29/it-asset-ticket-tracker/actions/workflows/tests.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Dependencies](https://img.shields.io/badge/NuGet%20packages-none-brightgreen)
![License](https://img.shields.io/badge/license-MIT-lightgrey)

A command-line IT asset register and service desk ticketing tool, built for a fictional company, **Shiningstar Logistics Ltd**, whose small IT team was tracking laptops in a spreadsheet and support requests by email. It models the day-to-day work of a service desk: logging and triaging incidents against priority-based SLAs, managing the asset lifecycle from stock to assignment, repair and retirement, and reporting on SLA performance and warranty risk.

## Features

**Asset management**
- Register laptops, desktops, monitors, phones, printers, network devices and servers
- Asset lifecycle: `InStock → Assigned → InRepair → Retired`, with reassignment and return-to-stock for joiners, movers and leavers
- Business rules: no duplicate tags or serial numbers, no assigning retired or in-repair kit, no retiring an asset with open tickets

**Ticketing**
- Incidents and requests with priorities P1-P4 and automatic SLA due times
- Enforced lifecycle: `New → InProgress → Resolved → Closed` (resolved tickets can be reopened; closed is final)
- Tickets must be assigned before work starts, and a resolution note is required to resolve
- Priority changes recalculate the SLA from the original logging time, so re-prioritising cannot hide a breach

**Reporting and audit**
- Service desk dashboard: assets by status, open tickets by priority, SLA breaches, SLA compliance %, warranties expiring in 90 days or already expired, and the assets generating the most tickets
- A timestamped, append-only history on every asset and ticket (who it was assigned to, status changes, notes)

## SLA targets

| Priority | Meaning | Resolution target |
|---|---|---|
| P1 | Critical: service or site down | 4 hours |
| P2 | High: user or team unable to work | 8 hours |
| P3 | Medium: degraded but workaround exists | 24 hours |
| P4 | Low: request or minor issue | 72 hours |

SLAs are measured in calendar hours for simplicity. A real service desk would usually measure P3/P4 in business hours.

## Demo

```text
$ assettracker demo
Loaded demo data: 7 assets, 4 tickets.

$ assettracker ticket list --open
ID        Pri  Status      Title                           Asset     Analyst     Due               SLA
--------------------------------------------------------------------------------------------------------
INC-0001  P2   InProgress  Cannot connect to VPN from ho~  LT-0002   it.support  2026-10-14 04:34  OK
INC-0002  P3   New         Printer jamming on every job    PR-0001   it.support  2026-10-14 20:34  OK
INC-0003  P4   New         Request: second monitor for f~  -         -           2026-10-16 20:34  OK

$ assettracker asset assign DT-0001 --to s.rose
Error: DT-0001 is in repair. Return it to stock first.

$ assettracker asset retire LT-0002 --reason "End of life"
Error: LT-0002 has open tickets (INC-0001). Resolve them before retiring.

$ assettracker report
IT SERVICE DESK REPORT
==================================================
Assets by status:
  InStock       2
  Assigned      4
  InRepair      1
  Retired       0

Open tickets by priority:
  P1 (4h SLA)      0
  P2 (8h SLA)      1
  P3 (24h SLA)     1
  P4 (72h SLA)     1

SLA breaches on open tickets: 0

SLA compliance (resolved tickets): 100% of 1

Warranties expiring in the next 90 days: 2
  PH-0001 iPhone 13 - 2026-11-02
  LT-0003 Lenovo ThinkPad T14 - 2026-11-27
Warranties already expired (active assets): 2
  DT-0001 HP EliteDesk 800 G6 - 2026-03-27
  PR-0001 HP LaserJet M404 - 2026-07-05
```

Demo dates are generated relative to the day you run `demo`, so your output will show different dates.

## Getting started

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). No NuGet packages are used.

```bash
git clone https://github.com/SnehaSukumar29/it-asset-ticket-tracker.git
cd it-asset-ticket-tracker
dotnet build -c Release
dotnet run --project src/AssetTracker.Cli -c Release -- --data demo.json demo
dotnet run --project src/AssetTracker.Cli -c Release -- --data demo.json report
```

To use it as a single command, publish it and add the output folder to your PATH:

```bash
dotnet publish src/AssetTracker.Cli -c Release -o ./publish
./publish/assettracker help
```

Data is stored in one JSON file: `--data FILE`, else the `ASSET_TRACKER_DATA` environment variable, else `%LOCALAPPDATA%\AssetTracker\tracker-data.json` on Windows (`~/.local/share/AssetTracker/` on Linux).

## Commands

| Command | Purpose |
|---|---|
| `asset add --tag LT-0001 --type Laptop --model "Dell Latitude 5440" --serial ABC123 --purchased 2025-01-15 --warranty 2028-01-15` | Register an asset |
| `asset list [--status Assigned] [--user s.rose]` | List and filter assets |
| `asset show LT-0001` | Asset details and full history |
| `asset assign LT-0001 --to s.rose` | Assign or reassign |
| `asset return LT-0001` | Return to stock (e.g. leaver) |
| `asset repair LT-0001 --reason "Cracked screen"` | Mark as in repair |
| `asset retire LT-0001 --reason "End of life"` | Retire (blocked if tickets are open) |
| `ticket new --title "VPN not connecting" --requester j.hopper --priority P2 --asset LT-0002 --desc "Error 809"` | Log a ticket |
| `ticket list [--open] [--priority P1] [--analyst it.support]` | Ticket queue, sorted by priority then due time |
| `ticket show INC-0001` | Ticket details and history |
| `ticket assign INC-0001 --to it.support` | Assign to an analyst |
| `ticket note INC-0001 --text "Checked event logs"` | Add a work note |
| `ticket priority INC-0001 P1 --reason "Whole team affected"` | Re-prioritise |
| `ticket status INC-0001 Resolved --resolution "Reset VPN profile"` | Move through the lifecycle |
| `report` | Service desk dashboard |

## Security and design decisions

| Concern | Approach |
|---|---|
| Untrusted input | Allow-list validation with regular expressions for asset tags (`LT-0001`), usernames (`s.rose`), serial numbers and ticket IDs. Free text is trimmed, length-limited and stripped of control characters (e.g. terminal escape sequences). |
| Enum tampering | `Enum.TryParse` accepts numeric strings such as `"99"`, so numeric input is explicitly rejected and parsed values are checked with `Enum.IsDefined`. |
| Audit trail | Every change is written to an append-only, UTC-timestamped history on the asset or ticket, supporting accountability and later review. |
| Data integrity | Saves go to a temporary file which then replaces the real file, so an interrupted save leaves the previous data intact. Corrupt or oversized (>50 MB) data files are refused with a clear error instead of crashing. |
| Process integrity | Lifecycle rules are enforced in code (no skipping from New to Closed, no work on unassigned tickets, no resolving without a resolution, no retiring assets with open tickets). |
| Testability | Time is injected through an `IClock` interface so SLA breaches and warranty windows are tested deterministically. |

## Project structure

```text
src/
├── AssetTracker.Core/          # class library: all business logic
│   ├── Models/                 # Asset, Ticket, HistoryEntry, enums
│   ├── Services/               # AssetService, TicketService, ReportService, Validate, IClock
│   └── Storage/                # JsonDataStore (System.Text.Json)
└── AssetTracker.Cli/           # console front end and demo data
tests/
└── AssetTracker.Tests/         # 24 tests with a small dependency-free test runner
```

The business logic lives in a separate class library with no console code, so a GUI or web front end could be added later without changing it.

## Running the tests

```bash
dotnet run --project tests/AssetTracker.Tests
```

The tests use a small built-in runner rather than xUnit so the project has no external dependencies. They also run on Windows and Linux on every push via GitHub Actions.

## Possible future improvements

- Business-hours SLA calculation with UK bank holidays
- CSV export of the asset register for audits
- Role-based access (requester vs analyst vs admin)
- A WPF or ASP.NET front end on top of `AssetTracker.Core`

## Licence

MIT. See [LICENSE](LICENSE).
