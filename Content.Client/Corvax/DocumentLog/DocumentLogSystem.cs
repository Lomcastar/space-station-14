using Content.Client.RoundEnd;
using Content.Shared.Corvax.DocumentLog;
using Robust.Client.UserInterface;

namespace Content.Client.Corvax.DocumentLog;

public sealed class DocumentLogSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public event Action<DocumentLogDetailsEvent>? DetailsReceived;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DocumentLogSnapshotEvent>(OnSnapshot);
        SubscribeNetworkEvent<DocumentLogDetailsEvent>(OnDetails);
    }

    private void OnSnapshot(DocumentLogSnapshotEvent ev)
    {
        _ui.GetUIController<RoundEndSummaryUIController>().SetDocumentLog(ev);
    }

    private void OnDetails(DocumentLogDetailsEvent ev)
    {
        DetailsReceived?.Invoke(ev);
    }

    public void RequestDetails(int recordId)
    {
        RaiseNetworkEvent(new DocumentLogDetailsRequestEvent { RecordId = recordId });
    }
}
