using AssetTracker.Core.Models;
using AssetTracker.Core.Services;
using AssetTracker.Core.Storage;

namespace AssetTracker.Tests;

public static class ReportAndStorageTests
{
    [Test]
    public static void Report_CountsWarrantiesSlaAndProblemAssets()
    {
        var (data, clock, assets, tickets) = Fixtures.Create();
        Fixtures.AddLaptop(assets, "LT-0001", "SER-1", warranty: "2026-11-01"); // 19 days away -> expiring soon
        Fixtures.AddLaptop(assets, "LT-0002", "SER-2", warranty: "2026-01-01"); // expired
        Fixtures.AddLaptop(assets, "LT-0003", "SER-3", warranty: "2025-06-01");
        assets.Retire("LT-0003", "Old");                                     // retired -> ignored
        tickets.Create("a", "s.rose", "P1", null, "LT-0001");
        tickets.Create("b", "s.rose", "P3", null, "LT-0001");
        var resolved = tickets.Create("c", "s.rose", "P3", null, "LT-0002");
        tickets.ChangeStatus(resolved.Id, "Resolved", "done");
        clock.UtcNow = Fixtures.Now.AddHours(6);                             // P1 now breached

        var r = new ReportService(data, clock).Build();
        Assert.Equal("LT-0001", r.WarrantyExpiringSoon.Single().Tag);
        Assert.Equal("LT-0002", r.WarrantyExpired.Single().Tag);
        Assert.Equal(1, r.OpenTicketsByPriority[TicketPriority.P1]);
        Assert.Equal("INC-0001", r.BreachedOpenTickets.Single().Id);
        Assert.Equal<double?>(100.0, r.SlaCompliancePercent);
        Assert.Equal(("LT-0001", 2), r.TopProblemAssets[0]);
        Assert.Equal(1, r.AssetsByStatus[AssetStatus.Retired]);
    }

    [Test]
    public static void Storage_RoundTripsAllData()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "data.json");
            var (data, _, assets, tickets) = Fixtures.Create();
            Fixtures.AddLaptop(assets);
            assets.Assign("LT-0001", "s.rose");
            tickets.Create("VPN", "s.rose", "P2", "details", "LT-0001");

            var store = new JsonDataStore(path);
            store.Save(data);
            var loaded = store.Load();

            Assert.Equal(2, loaded.NextTicketNumber);
            Assert.Equal("s.rose", loaded.Assets.Single().AssignedTo);
            Assert.Equal(new DateOnly(2028, 1, 1), loaded.Assets.Single().WarrantyExpiry!.Value);
            Assert.Equal(TicketPriority.P2, loaded.Tickets.Single().Priority);
            Assert.Equal(2, loaded.Assets.Single().History.Count);
            Assert.True(!File.Exists(path + ".tmp"), "Temp file should be cleaned up");
            Assert.True(File.ReadAllText(path).Contains("\"Priority\": \"P2\""), "Enums stored as readable strings");
        }
        finally { dir.Delete(recursive: true); }
    }

    [Test]
    public static void Storage_MissingFileGivesEmptyData()
    {
        var store = new JsonDataStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Equal(0, store.Load().Assets.Count);
    }

    [Test]
    public static void Storage_CorruptFileGivesClearError()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{ this is not json");
        try { Assert.Throws<InvalidDataException>(() => new JsonDataStore(path).Load()); }
        finally { File.Delete(path); }
    }
}
