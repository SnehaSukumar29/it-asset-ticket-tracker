using AssetTracker.Core.Models;
using AssetTracker.Core.Services;

namespace AssetTracker.Tests;

public static class Fixtures
{
    public static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);

    public static (TrackerData Data, FixedClock Clock, AssetService Assets, TicketService Tickets) Create()
    {
        var data = new TrackerData();
        var clock = new FixedClock(Now);
        return (data, clock, new AssetService(data, clock), new TicketService(data, clock));
    }

    public static Asset AddLaptop(AssetService assets, string tag = "LT-0001", string serial = "SER001",
        string purchased = "2025-01-01", string? warranty = "2028-01-01") =>
        assets.Add(tag, "Laptop", "Dell Latitude 5440", serial, purchased, warranty);
}
