namespace AssetTracker.Core.Models;

public enum AssetType { Laptop, Desktop, Monitor, Phone, Tablet, Printer, NetworkDevice, Server, Other }

public enum AssetStatus { InStock, Assigned, InRepair, Retired }

/// <summary>P1 = critical (whole site / service down) ... P4 = low (request or minor issue).</summary>
public enum TicketPriority { P1, P2, P3, P4 }

public enum TicketStatus { New, InProgress, Resolved, Closed }
