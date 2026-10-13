using AssetTracker.Core.Models;
using AssetTracker.Core.Services;

namespace AssetTracker.Tests;

public static class AssetServiceTests
{
    [Test]
    public static void Add_CreatesAssetInStockWithHistory()
    {
        var (_, _, assets, _) = Fixtures.Create();
        var a = Fixtures.AddLaptop(assets);
        Assert.Equal(AssetStatus.InStock, a.Status);
        Assert.Equal(1, a.History.Count);
    }

    [Test]
    public static void Add_RejectsDuplicateTagAndSerial()
    {
        var (_, _, assets, _) = Fixtures.Create();
        Fixtures.AddLaptop(assets);
        Assert.Throws<TrackerException>(() => Fixtures.AddLaptop(assets, tag: "LT-0001", serial: "OTHER1"));
        Assert.Throws<TrackerException>(() => Fixtures.AddLaptop(assets, tag: "LT-0002", serial: "SER001"));
    }

    [Test]
    public static void Add_RejectsFutureAndInconsistentDates()
    {
        var (_, _, assets, _) = Fixtures.Create();
        Assert.Throws<TrackerException>(() => Fixtures.AddLaptop(assets, purchased: "2027-01-01"));
        Assert.Throws<TrackerException>(() => Fixtures.AddLaptop(assets, purchased: "2025-01-01", warranty: "2024-01-01"));
    }

    [Test]
    public static void Assign_Reassign_ReturnToStock_TrackHistory()
    {
        var (_, _, assets, _) = Fixtures.Create();
        Fixtures.AddLaptop(assets);
        assets.Assign("LT-0001", "s.rose");
        var a = assets.Assign("LT-0001", "j.hopper");
        Assert.Equal("j.hopper", a.AssignedTo);
        Assert.True(a.History.Last().Detail.Contains("from s.rose to j.hopper"));
        a = assets.ReturnToStock("LT-0001");
        Assert.Equal(AssetStatus.InStock, a.Status);
        Assert.Equal<string?>(null, a.AssignedTo);
        Assert.Equal(4, a.History.Count);
    }

    [Test]
    public static void Assign_BlockedForRepairAndRetiredAssets()
    {
        var (_, _, assets, _) = Fixtures.Create();
        Fixtures.AddLaptop(assets);
        Fixtures.AddLaptop(assets, "LT-0002", "SER002");
        assets.SendForRepair("LT-0001", "Broken hinge");
        assets.Retire("LT-0002", "End of life");
        Assert.Throws<TrackerException>(() => assets.Assign("LT-0001", "s.rose"));
        Assert.Throws<TrackerException>(() => assets.Assign("LT-0002", "s.rose"));
    }

    [Test]
    public static void Retire_BlockedWhileTicketsAreOpen()
    {
        var (_, _, assets, tickets) = Fixtures.Create();
        Fixtures.AddLaptop(assets);
        var t = tickets.Create("Screen flicker", "s.rose", "P3", null, "LT-0001");
        var ex = Assert.Throws<TrackerException>(() => assets.Retire("LT-0001", "Old"));
        Assert.True(ex.Message.Contains(t.Id));
        tickets.ChangeStatus(t.Id, "Resolved", "Replaced screen");
        Assert.Equal(AssetStatus.Retired, assets.Retire("LT-0001", "Old").Status);
    }

    [Test]
    public static void List_FiltersByStatusAndUser()
    {
        var (_, _, assets, _) = Fixtures.Create();
        Fixtures.AddLaptop(assets);
        Fixtures.AddLaptop(assets, "LT-0002", "SER002");
        assets.Assign("LT-0002", "s.rose");
        Assert.Equal(1, assets.List(status: "InStock").Count);
        Assert.Equal("LT-0002", assets.List(assignedTo: "s.rose").Single().Tag);
    }
}
