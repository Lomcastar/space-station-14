namespace Content.Shared.Corvax.DocumentLog;

/// <summary>
/// Links a paper to its record in the document log. The paper carries only the number.
/// </summary>
[RegisterComponent]
public sealed partial class DocumentRecordComponent : Component
{
    [ViewVariables]
    public int RecordId;
}
