using Robust.Shared.Prototypes;

namespace Content.Shared.Corvax.DocumentLog;

/// <summary>
/// How the document log treats a rubber stamp. The ID must match the stamp entity prototype.
/// </summary>
[Prototype("documentStamp")]
public sealed partial class DocumentStampPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public DocumentStampKind Kind = DocumentStampKind.Stamp;

    [DataField]
    public List<ProtoId<DocumentCategoryPrototype>> Categories = new();
}

public enum DocumentStampKind : byte
{
    Stamp,
    Approve,
    Deny,
}
