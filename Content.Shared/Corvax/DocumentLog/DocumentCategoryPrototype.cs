using Robust.Shared.Prototypes;

namespace Content.Shared.Corvax.DocumentLog;

/// <summary>
/// Category of a stamp or a person in the document log. Mirrors the game departments
/// plus a few that are not departments (Other, Syndicate, Wizard), so it is its own prototype.
/// </summary>
[Prototype("documentCategory")]
public sealed partial class DocumentCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public Color Color;

    [DataField]
    public int Weight;
}
