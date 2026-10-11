using Content.Client.GameTicking.Managers;
using Content.Shared.Corvax.DocumentLog; // Corvax-DocumentLog
using Content.Shared.GameTicking;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client.RoundEnd;

[UsedImplicitly]
public sealed partial class RoundEndSummaryUIController : UIController,
    IOnSystemLoaded<ClientGameTicker>
{
    [Dependency] private IInputManager _input = default!;

    private RoundEndSummaryWindow? _window;
    private DocumentLogSnapshotEvent? _documentLog; // Corvax-DocumentLog

    private void ToggleScoreboardWindow(ICommonSession? session = null)
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
        }
        else
        {
            _window.OpenCenteredRight();
            _window.MoveToFront();
        }
    }

    public void OpenRoundEndSummaryWindow(RoundEndMessageEvent message)
    {
        // Don't open duplicate windows (mainly for replays).
        if (_window?.RoundId == message.RoundId)
            return;

        _window = new RoundEndSummaryWindow(message.GamemodeTitle, message.RoundEndText,
            message.RoundDuration, message.RoundId, message.AllPlayersEndInfo);

        // Corvax-DocumentLog-Start
        if (_documentLog?.RoundId == message.RoundId)
            _window.AddDocumentLogTab(_documentLog);
        // Corvax-DocumentLog-End
    }

    // Corvax-DocumentLog-Start
    public void SetDocumentLog(DocumentLogSnapshotEvent snapshot)
    {
        _documentLog = snapshot;
        if (_window?.RoundId == snapshot.RoundId)
            _window.AddDocumentLogTab(snapshot);
    }
    // Corvax-DocumentLog-End

    public void OnSystemLoaded(ClientGameTicker system)
    {
        _input.SetInputCommand(ContentKeyFunctions.ToggleRoundEndSummaryWindow,
            InputCmdHandler.FromDelegate(ToggleScoreboardWindow));
    }
}
