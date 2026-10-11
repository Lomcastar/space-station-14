using Content.Shared.Paper;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.DocumentLog;

public enum DocumentEventType : byte
{
    Stamped,
    Marked,
    Forged,
    Sealed,
    Torn,
    Destroyed,
    Sent,
    Copied,
    Received,
    Printed,
}

public enum DocumentFate : byte
{
    Intact,
    Burned,
    Disposed,
    Destroyed,
    SentToCentcom,
    EvacuatedToCentcom,
}

[Serializable, NetSerializable]
public sealed class DocumentLogPersonInfo
{
    public string Name = string.Empty;
    public string JobTitle = string.Empty;
    public string Category = string.Empty;
    public List<string> Categories = new();
}

[Serializable, NetSerializable]
public sealed class DocumentLogEventInfo
{
    public TimeSpan Time;
    public DocumentEventType Type;
    public DocumentLogPersonInfo? Actor;
    public string? Stamp;
    public DocumentStampKind StampKind;
    public bool Retro;
    public string? FaxFrom;
    public string? FaxTo;
}

[Serializable, NetSerializable]
public sealed class DocumentLogRecordInfo
{
    public int Id;
    public TimeSpan CreatedAt;
    public string Title = string.Empty;
    public DocumentLogPersonInfo Author = new();
    public int? ParentId;
    public List<DocumentLogEventInfo> Events = new();
    public int ReaderCount;
    public DocumentFate Fate;
}

/// <summary>
/// Sent to every client at round end. Carries metadata only; text and readers come on request.
/// </summary>
[Serializable, NetSerializable]
public sealed class DocumentLogSnapshotEvent : EntityEventArgs
{
    public int RoundId;
    public TimeSpan RoundDuration;
    public List<DocumentLogRecordInfo> Records = new();
}

[Serializable, NetSerializable]
public sealed class DocumentLogDetailsRequestEvent : EntityEventArgs
{
    public int RecordId;
}

[Serializable, NetSerializable]
public sealed class DocumentLogDetailsEvent : EntityEventArgs
{
    public int RecordId;
    public string Text = string.Empty;
    public List<StampDisplayInfo> Stamps = new();
    public List<DocumentLogPersonInfo> Readers = new();
}
