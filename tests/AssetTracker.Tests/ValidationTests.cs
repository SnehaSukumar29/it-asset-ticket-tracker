using AssetTracker.Core.Models;
using AssetTracker.Core.Services;

namespace AssetTracker.Tests;

public static class ValidationTests
{
    [Test]
    public static void AssetTag_IsNormalisedToUppercase() => Assert.Equal("LT-0001", Validate.AssetTag(" lt-0001 "));

    [Test]
    public static void AssetTag_RejectsBadFormats()
    {
        foreach (var bad in new[] { "", "LT0001", "LAPTOP-0001", "LT-01", "LT-0001; rm -rf /" })
            Assert.Throws<TrackerException>(() => Validate.AssetTag(bad));
    }

    [Test]
    public static void Username_RejectsInjectionAndSpaces()
    {
        foreach (var bad in new[] { "", "a", "Robert'); DROP TABLE users;--", "john smith", "<script>", "1user" })
            Assert.Throws<TrackerException>(() => Validate.Username(bad));
        Assert.Equal("s.rose", Validate.Username("S.Rose"));
    }

    [Test]
    public static void Enum_RejectsNumericAndUnknownValues()
    {
        Assert.Throws<TrackerException>(() => Validate.Enum<TicketPriority>("99", "Priority"));
        Assert.Throws<TrackerException>(() => Validate.Enum<TicketPriority>("0", "Priority"));
        Assert.Throws<TrackerException>(() => Validate.Enum<TicketPriority>("P9", "Priority"));
        Assert.Equal(TicketPriority.P2, Validate.Enum<TicketPriority>("p2", "Priority"));
    }

    [Test]
    public static void Text_StripsControlCharactersAndEnforcesLength()
    {
        Assert.Equal("hello[31mworld", Validate.Text("hello\u001b[31mworld", "Note", 50));
        Assert.Throws<TrackerException>(() => Validate.Text(new string('x', 51), "Note", 50));
        Assert.Throws<TrackerException>(() => Validate.Text("   ", "Note", 50));
        Assert.Equal("", Validate.Text(null, "Note", 50, required: false));
    }

    [Test]
    public static void Date_RequiresIsoFormat()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), Validate.Date("2026-02-28", "Date"));
        Assert.Throws<TrackerException>(() => Validate.Date("28/02/2026", "Date"));
        Assert.Throws<TrackerException>(() => Validate.Date("2026-02-30", "Date"));
    }
}
