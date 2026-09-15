using Content.Shared.Fax.Components;

namespace Content.Shared.Corvax.DocumentLog;

[ByRefEvent]
public readonly record struct FaxDocumentSentEvent(EntityUid Fax, EntityUid Paper, EntityUid Actor, string? FromName, string? ToName);

[ByRefEvent]
public readonly record struct FaxDocumentCopiedEvent(EntityUid Fax, EntityUid Paper, EntityUid Actor, string? FaxName);

/// <summary>
/// Raised on a paper a fax has just printed, whether from a copy job or a received transmission.
/// </summary>
[ByRefEvent]
public readonly record struct FaxPaperPrintedEvent(EntityUid Fax, EntityUid Paper, FaxPrintout Printout);
