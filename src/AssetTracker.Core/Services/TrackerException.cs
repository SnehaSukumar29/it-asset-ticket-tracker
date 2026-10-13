namespace AssetTracker.Core.Services;

/// <summary>A business-rule or validation failure with a message safe to show the user.</summary>
public sealed class TrackerException(string message) : Exception(message);
