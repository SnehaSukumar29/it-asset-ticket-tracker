namespace AssetTracker.Core.Models;

/// <summary>An append-only audit trail entry recorded for every change.</summary>
public sealed record HistoryEntry(DateTime TimestampUtc, string Action, string Detail);
