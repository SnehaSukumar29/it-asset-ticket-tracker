using AssetTracker.Core.Models;

namespace AssetTracker.Core.Services;

public sealed class TicketService(TrackerData data, IClock clock)
{
    /// <summary>Resolution targets in calendar hours (a simplification of business-hours SLAs).</summary>
    public static readonly IReadOnlyDictionary<TicketPriority, int> SlaHours = new Dictionary<TicketPriority, int>
    {
        [TicketPriority.P1] = 4,
        [TicketPriority.P2] = 8,
        [TicketPriority.P3] = 24,
        [TicketPriority.P4] = 72,
    };

    // Allowed lifecycle moves. Closed is final; Resolved can be reopened.
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Transitions = new()
    {
        [TicketStatus.New] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.InProgress] = [TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [],
    };

    public Ticket Create(string title, string requester, string priority, string? description = null, string? assetTag = null)
    {
        string? tag = null;
        if (!string.IsNullOrWhiteSpace(assetTag))
        {
            tag = Validate.AssetTag(assetTag);
            var asset = data.Assets.FirstOrDefault(a => a.Tag == tag)
                ?? throw new TrackerException($"Asset {tag} not found.");
            if (asset.Status == AssetStatus.Retired)
                throw new TrackerException($"Asset {tag} is retired; tickets cannot be raised against it.");
        }

        var p = Validate.Enum<TicketPriority>(priority, "Priority");
        var now = clock.UtcNow;
        var ticket = new Ticket
        {
            Id = $"INC-{data.NextTicketNumber:D4}",
            Title = Validate.Text(title, "Title", 100),
            Description = Validate.Text(description, "Description", 1000, required: false),
            Requester = Validate.Username(requester),
            Priority = p,
            AssetTag = tag,
            CreatedUtc = now,
            DueUtc = now.AddHours(SlaHours[p]),
        };
        data.NextTicketNumber++;
        Record(ticket, "Created", $"{p} ticket logged by {ticket.Requester}");
        data.Tickets.Add(ticket);
        return ticket;
    }

    public Ticket Get(string id)
    {
        var tid = Validate.TicketId(id);
        return data.Tickets.FirstOrDefault(t => t.Id == tid)
            ?? throw new TrackerException($"Ticket {tid} not found.");
    }

    public IReadOnlyList<Ticket> List(bool openOnly = false, string? priority = null, string? assignedTo = null)
    {
        IEnumerable<Ticket> q = data.Tickets;
        if (openOnly) q = q.Where(t => t.IsOpen);
        if (!string.IsNullOrWhiteSpace(priority))
        {
            var p = Validate.Enum<TicketPriority>(priority, "Priority");
            q = q.Where(t => t.Priority == p);
        }
        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            var u = Validate.Username(assignedTo);
            q = q.Where(t => t.AssignedTo == u);
        }
        return q.OrderBy(t => t.Priority).ThenBy(t => t.DueUtc).ToList();
    }

    public Ticket Assign(string id, string analyst)
    {
        var t = Get(id);
        EnsureNotClosed(t);
        var user = Validate.Username(analyst);
        var previous = t.AssignedTo;
        t.AssignedTo = user;
        Record(t, "Assigned", previous is null ? $"Assigned to {user}" : $"Reassigned from {previous} to {user}");
        return t;
    }

    public Ticket AddNote(string id, string note)
    {
        var t = Get(id);
        EnsureNotClosed(t);
        Record(t, "Note", Validate.Text(note, "Note", 500));
        return t;
    }

    /// <summary>Change priority. The SLA due time is recalculated from the original creation time.</summary>
    public Ticket ChangePriority(string id, string priority, string reason)
    {
        var t = Get(id);
        if (!t.IsOpen) throw new TrackerException($"{t.Id} is not open; priority can only be changed on open tickets.");
        var p = Validate.Enum<TicketPriority>(priority, "Priority");
        var why = Validate.Text(reason, "Reason", 200);
        if (p == t.Priority) throw new TrackerException($"{t.Id} is already {p}.");
        var old = t.Priority;
        t.Priority = p;
        t.DueUtc = t.CreatedUtc.AddHours(SlaHours[p]);
        Record(t, "Priority", $"{old} -> {p}: {why}");
        return t;
    }

    public Ticket ChangeStatus(string id, string status, string? resolution = null)
    {
        var t = Get(id);
        var next = Validate.Enum<TicketStatus>(status, "Status");
        if (!Transitions[t.Status].Contains(next))
            throw new TrackerException($"{t.Id} cannot move from {t.Status} to {next}.");

        if (next == TicketStatus.InProgress && t.AssignedTo is null)
            throw new TrackerException($"Assign {t.Id} to an analyst before starting work on it.");

        var old = t.Status;
        switch (next)
        {
            case TicketStatus.Resolved:
                t.Resolution = Validate.Text(resolution, "Resolution", 500);
                t.ResolvedUtc = clock.UtcNow;
                break;
            case TicketStatus.InProgress when old == TicketStatus.Resolved: // reopened
                t.ResolvedUtc = null;
                t.Resolution = null;
                break;
        }
        t.Status = next;
        Record(t, "Status", next == TicketStatus.Resolved ? $"{old} -> {next}: {t.Resolution}" : $"{old} -> {next}");
        return t;
    }

    public bool IsBreached(Ticket t) =>
        t.IsOpen ? clock.UtcNow > t.DueUtc : t.ResolvedUtc > t.DueUtc;

    private static void EnsureNotClosed(Ticket t)
    {
        if (t.Status == TicketStatus.Closed)
            throw new TrackerException($"{t.Id} is closed.");
    }

    private void Record(Ticket t, string action, string detail) =>
        t.History.Add(new HistoryEntry(clock.UtcNow, action, detail));
}
