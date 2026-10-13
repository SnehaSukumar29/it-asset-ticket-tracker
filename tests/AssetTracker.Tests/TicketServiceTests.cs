using AssetTracker.Core.Models;
using AssetTracker.Core.Services;

namespace AssetTracker.Tests;

public static class TicketServiceTests
{
    [Test]
    public static void Create_SetsSequentialIdsAndSlaDueTime()
    {
        var (_, _, _, tickets) = Fixtures.Create();
        var t1 = tickets.Create("Email down", "s.rose", "P1");
        var t2 = tickets.Create("New mouse", "s.rose", "P4");
        Assert.Equal("INC-0001", t1.Id);
        Assert.Equal("INC-0002", t2.Id);
        Assert.Equal(Fixtures.Now.AddHours(4), t1.DueUtc);
        Assert.Equal(Fixtures.Now.AddHours(72), t2.DueUtc);
    }

    [Test]
    public static void Create_RejectsUnknownOrRetiredAsset()
    {
        var (_, _, assets, tickets) = Fixtures.Create();
        Assert.Throws<TrackerException>(() => tickets.Create("x", "s.rose", "P3", null, "LT-9999"));
        Fixtures.AddLaptop(assets);
        assets.Retire("LT-0001", "Old");
        Assert.Throws<TrackerException>(() => tickets.Create("x", "s.rose", "P3", null, "LT-0001"));
    }

    [Test]
    public static void Lifecycle_FollowsAllowedTransitionsOnly()
    {
        var (_, _, _, tickets) = Fixtures.Create();
        var t = tickets.Create("VPN", "j.hopper", "P2");
        Assert.Throws<TrackerException>(() => tickets.ChangeStatus(t.Id, "Closed"));      // New -> Closed not allowed
        Assert.Throws<TrackerException>(() => tickets.ChangeStatus(t.Id, "InProgress"));  // must be assigned first
        tickets.Assign(t.Id, "it.support");
        tickets.ChangeStatus(t.Id, "InProgress");
        Assert.Throws<TrackerException>(() => tickets.ChangeStatus(t.Id, "Resolved"));    // resolution text required
        tickets.ChangeStatus(t.Id, "Resolved", "Reset VPN profile");
        tickets.ChangeStatus(t.Id, "Closed");
        Assert.Throws<TrackerException>(() => tickets.ChangeStatus(t.Id, "InProgress"));  // closed is final
        Assert.Throws<TrackerException>(() => tickets.AddNote(t.Id, "late note"));
    }

    [Test]
    public static void Reopen_ClearsResolution()
    {
        var (_, _, _, tickets) = Fixtures.Create();
        var t = tickets.Create("VPN", "j.hopper", "P2");
        tickets.Assign(t.Id, "it.support");
        tickets.ChangeStatus(t.Id, "Resolved", "Fixed");
        tickets.ChangeStatus(t.Id, "InProgress");
        Assert.Equal<string?>(null, t.Resolution);
        Assert.Equal<DateTime?>(null, t.ResolvedUtc);
        Assert.True(t.IsOpen);
    }

    [Test]
    public static void SlaBreach_DetectedForOpenAndResolvedTickets()
    {
        var (_, clock, _, tickets) = Fixtures.Create();
        var late = tickets.Create("Server down", "s.rose", "P1");
        var onTime = tickets.Create("Printer", "s.rose", "P1");
        clock.UtcNow = Fixtures.Now.AddHours(3);
        tickets.ChangeStatus(onTime.Id, "Resolved", "Cleared queue");
        Assert.True(!tickets.IsBreached(late), "Not yet breached at 3h");
        clock.UtcNow = Fixtures.Now.AddHours(5);
        Assert.True(tickets.IsBreached(late), "Breached at 5h");
        Assert.True(!tickets.IsBreached(onTime), "Resolved inside SLA stays compliant");
        tickets.ChangeStatus(late.Id, "Resolved", "Rebooted");
        Assert.True(tickets.IsBreached(late), "Resolved after due time counts as breach");
    }

    [Test]
    public static void ChangePriority_RecalculatesDueFromCreation()
    {
        var (_, clock, _, tickets) = Fixtures.Create();
        var t = tickets.Create("Slow PC", "s.jones", "P4");
        clock.UtcNow = Fixtures.Now.AddHours(1);
        tickets.ChangePriority(t.Id, "P2", "Affects payroll run");
        Assert.Equal(Fixtures.Now.AddHours(8), t.DueUtc);
        Assert.Throws<TrackerException>(() => tickets.ChangePriority(t.Id, "P2", "same"));
    }

    [Test]
    public static void List_OrdersByPriorityThenDue()
    {
        var (_, _, _, tickets) = Fixtures.Create();
        tickets.Create("c", "s.rose", "P4");
        tickets.Create("a", "s.rose", "P1");
        tickets.Create("b", "s.rose", "P3");
        var titles = string.Join("", tickets.List().Select(t => t.Title));
        Assert.Equal("abc", titles);
    }
}
