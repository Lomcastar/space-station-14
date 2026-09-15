namespace Content.Shared.Corvax.DocumentLog;

/// <summary>
/// Raised on a paper after a player stamps it by hand. Not raised when a fax reapplies stamps to a copy.
/// </summary>
[ByRefEvent]
public readonly record struct PaperStampedEvent(EntityUid Paper, EntityUid User, EntityUid Stamp);
