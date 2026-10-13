using System.Text.RegularExpressions;

namespace AssetTracker.Core.Services;

/// <summary>Allow-list input validation. Everything entering the data store goes through here.</summary>
public static partial class Validate
{
    [GeneratedRegex(@"^[A-Z]{2,4}-\d{4}$")]
    private static partial Regex AssetTagRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9._-]{1,30}$")]
    private static partial Regex UsernameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9-]{3,40}$")]
    private static partial Regex SerialRegex();

    [GeneratedRegex(@"^INC-\d{4,}$")]
    private static partial Regex TicketIdRegex();

    public static string AssetTag(string? value)
    {
        var v = (value ?? "").Trim().ToUpperInvariant();
        if (!AssetTagRegex().IsMatch(v))
            throw new TrackerException($"Asset tag '{value}' is invalid. Use 2-4 letters, a dash and 4 digits, e.g. LT-0001.");
        return v;
    }

    public static string Username(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        if (!UsernameRegex().IsMatch(v))
            throw new TrackerException($"Username '{value}' is invalid. Use lowercase letters, digits, '.', '_' or '-', e.g. s.rose.");
        return v;
    }

    public static string Serial(string? value)
    {
        var v = (value ?? "").Trim().ToUpperInvariant();
        if (!SerialRegex().IsMatch(v))
            throw new TrackerException($"Serial number '{value}' is invalid. Use 3-40 letters, digits or dashes.");
        return v;
    }

    public static string TicketId(string? value)
    {
        var v = (value ?? "").Trim().ToUpperInvariant();
        if (!TicketIdRegex().IsMatch(v))
            throw new TrackerException($"Ticket ID '{value}' is invalid. Use the format INC-0001.");
        return v;
    }

    /// <summary>Required free text: trimmed, control characters removed, length-limited.</summary>
    public static string Text(string? value, string field, int maxLength, bool required = true)
    {
        var cleaned = new string((value ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (required && cleaned.Length == 0)
            throw new TrackerException($"{field} is required.");
        if (cleaned.Length > maxLength)
            throw new TrackerException($"{field} must be {maxLength} characters or fewer.");
        return cleaned;
    }

    public static TEnum Enum<TEnum>(string? value, string field) where TEnum : struct, System.Enum
    {
        // Reject numeric strings: Enum.TryParse would otherwise accept "99" as a valid value.
        if (!string.IsNullOrWhiteSpace(value) && !value.Trim().All(char.IsDigit)
            && System.Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var result)
            && System.Enum.IsDefined(result))
            return result;
        throw new TrackerException($"{field} '{value}' is invalid. Valid values: {string.Join(", ", System.Enum.GetNames<TEnum>())}.");
    }

    public static DateOnly Date(string? value, string field)
    {
        if (DateOnly.TryParseExact((value ?? "").Trim(), "yyyy-MM-dd", out var date))
            return date;
        throw new TrackerException($"{field} '{value}' is invalid. Use YYYY-MM-DD.");
    }
}
