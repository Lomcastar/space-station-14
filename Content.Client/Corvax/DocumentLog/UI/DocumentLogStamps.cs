using System.Numerics;
using Content.Shared.Corvax.DocumentLog;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.Corvax.DocumentLog.UI;

/// <summary>
/// The stamps and marks of a document family as a row of dots, in ink colours.
/// </summary>
public sealed class DocumentLogStamps : Control
{
    private const int MaxDots = 5;
    private const float Radius = 5.5f;
    private const float Step = 15f;

    private static readonly Color Ring = Color.White.WithAlpha(0.35f);
    private static readonly Color ApproveColor = Color.FromHex("#2F8F4F");
    private static readonly Color DenyColor = Color.FromHex("#B2403A");

    private readonly DocumentLogView _view;
    private readonly List<DocumentLogEventInfo> _events;

    public DocumentLogStamps(DocumentLogView view, List<DocumentLogEventInfo> events)
    {
        _view = view;
        _events = events;
        MinSize = new Vector2(MaxDots * Step + 4, 16);
        MaxWidth = MinSize.X;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        var y = PixelHeight / 2f;
        for (var i = 0; i < _events.Count && i < MaxDots; i++)
        {
            var pos = new Vector2((Radius + 1 + i * Step) * scale, y);
            DrawEvent(handle, _view, _events[i], pos, Radius * scale);
        }
    }

    public static void DrawEvent(DrawingHandleScreen handle, DocumentLogView view, DocumentLogEventInfo ev, Vector2 pos, float radius)
    {
        if (ev.Type == DocumentEventType.Stamped)
        {
            handle.DrawCircle(pos, radius, view.StampColor(ev.Stamp));
            handle.DrawCircle(pos, radius, Ring, false);
            return;
        }

        var color = view.IsMark(ev, DocumentStampKind.Approve) ? ApproveColor : DenyColor;
        handle.DrawCircle(pos, radius, color, !ev.Retro);
    }
}
