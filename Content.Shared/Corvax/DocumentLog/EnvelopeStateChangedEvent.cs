using Content.Shared.Paper;

namespace Content.Shared.Corvax.DocumentLog;

/// <summary>
/// Raised on an envelope right after it is sealed or torn, while the letter is still inside.
/// </summary>
[ByRefEvent]
public readonly record struct EnvelopeStateChangedEvent(EntityUid User, EnvelopeComponent.EnvelopeState State);
