using Content.Shared.Corvax.DocumentLog;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.DocumentLog;

public sealed class DocumentRecord
{
    public int Id;
    public TimeSpan CreatedAt;
    public string Title = string.Empty;
    public DocumentPerson Author = default!;
    public int? ParentId;
    public List<DocumentEvent> Events = new();
    public List<DocumentReader> Readers = new();
    public string Text = string.Empty;
    public DocumentFate Fate = DocumentFate.Intact;
}

/// <summary>
/// Who somebody was at the moment of an event. Never updated afterwards.
/// </summary>
public sealed record DocumentPerson(
    string Name,
    string JobTitle,
    ProtoId<DocumentCategoryPrototype> Category,
    List<ProtoId<DocumentCategoryPrototype>> Categories);

public sealed record DocumentReader(DocumentPerson Person, TimeSpan Time);

public sealed class DocumentEvent
{
    public TimeSpan Time;
    public DocumentEventType Type;
    public DocumentPerson? Actor;
    public EntProtoId? Stamp;
    public DocumentStampKind StampKind;
    public string? TextBefore;
    public string? FaxFrom;
    public string? FaxTo;

    /// <summary>
    /// The mark was already on the paper when the record was created; who and when is unknown.
    /// </summary>
    public bool Retro;
}

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
