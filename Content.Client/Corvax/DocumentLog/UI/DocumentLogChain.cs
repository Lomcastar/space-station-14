using System.Linq;
using System.Numerics;
using Content.Shared.Corvax.DocumentLog;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Corvax.DocumentLog.UI;

/// <summary>
/// History of one document family as a vertical list of events, one per line, in time order.
/// A narrow gutter on the left holds one lane per record; a copy branches off the lane of the
/// event that produced it. Grows downwards, so any number of events fits.
/// </summary>
public sealed class DocumentLogChain : BoxContainer
{
    private const float LaneStep = 16f;
    private const float GutterPad = 8f;
    private const float Radius = 5f;

    private static readonly Color LineColor = Color.FromHex("#5A5D70");
    private static readonly Color BranchColor = Color.FromHex("#7D8196");
    private static readonly Color LightColor = Color.FromHex("#D9DBE3");
    private static readonly Color MutedColor = Color.FromHex("#8E93A3");
    private static readonly Color PanelColor = Color.FromHex("#30313E");
    private static readonly Color RedColor = Color.FromHex("#E0645D");

    private readonly DocumentLogView _view;
    private readonly List<(Control Row, DocumentLogEventInfo Event, int Lane)> _rows = new();
    private readonly List<(int Lane, int? ParentLane, DocumentLogRecordInfo Record)> _lanes = new();

    public DocumentLogChain(DocumentLogView view, List<(DocumentLogRecordInfo Record, int Depth)> family)
    {
        _view = view;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        var laneOf = new Dictionary<int, int>();
        for (var i = 0; i < family.Count; i++)
            laneOf[family[i].Record.Id] = i;
        foreach (var (record, _) in family)
        {
            int? parentLane = record.ParentId is { } parent && laneOf.TryGetValue(parent, out var p) ? p : null;
            _lanes.Add((laneOf[record.Id], parentLane, record));
        }

        var gutter = GutterPad * 2 + (family.Count - 1) * LaneStep;
        var events = family
            .SelectMany(f => f.Record.Events.Select((ev, i) => (Record: f.Record, Event: ev, Index: i)))
            .OrderBy(n => n.Event.Time)
            .ThenBy(n => laneOf[n.Record.Id])
            .ThenBy(n => n.Index);

        foreach (var (record, ev, _) in events)
        {
            var row = MakeRow(record, ev, gutter);
            _rows.Add((row, ev, laneOf[record.Id]));
            AddChild(row);
        }
    }

    private Control MakeRow(DocumentLogRecordInfo record, DocumentLogEventInfo ev, float gutter)
    {
        var what = _view.Describe(ev);
        var who = ev.Actor != null
            ? DocumentLogView.FormatPerson(ev.Actor)
            : ev.Retro ? Loc.GetString("document-log-unknown-actor") : string.Empty;

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            MinHeight = 22,
            ToolTip = who.Length == 0 ? what : $"{what} — {who}",
            MouseFilter = MouseFilterMode.Pass,
        };
        row.AddChild(new Control { MinWidth = gutter, MaxWidth = gutter });
        row.AddChild(new Label { Text = DocumentLogView.FormatTime(ev.Time), MinWidth = 64, StyleClasses = { "monospace" }, FontColorOverride = MutedColor });
        row.AddChild(new Label { Text = Loc.GetString("document-log-lane", ("id", record.Id)), MinWidth = 30, StyleClasses = { "LabelSmall" }, FontColorOverride = MutedColor });
        row.AddChild(new Label { Text = what, ClipText = true, HorizontalExpand = true });
        row.AddChild(new Label { Text = who, ClipText = true, HorizontalExpand = true, SizeFlagsStretchRatio = 1.2f, FontColorOverride = MutedColor });
        return row;
    }

    private float LaneX(int lane) => (GutterPad + lane * LaneStep) * UIScale;

    private float RowY(Control row) => row.PixelPosition.Y + row.PixelHeight / 2f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;

        foreach (var (lane, parentLane, record) in _lanes)
        {
            var rows = _rows.Where(r => r.Lane == lane).ToList();
            if (rows.Count == 0)
                continue;

            var x = LaneX(lane);
            var top = RowY(rows[0].Row);
            var last = rows[^1];
            var bottom = RowY(last.Row);
            handle.DrawLine(new Vector2(x, top), new Vector2(x, bottom), LineColor);
            if (last.Event.Type != DocumentEventType.Destroyed)
                handle.DrawDottedLine(new Vector2(x, bottom), new Vector2(x, bottom + 9 * s), LineColor, 0f, 2f * s, 2f * s);

            if (parentLane is not { } parent)
                continue;

            // A copy branches from the last fax or copy event of its parent before the copy appeared.
            var start = rows[0].Event.Time;
            var sources = _rows.Where(r => r.Lane == parent && r.Event.Time <= start).ToList();
            var source = sources.LastOrDefault(r => r.Event.Type is DocumentEventType.Sent or DocumentEventType.Copied);
            if (source.Row == null && sources.Count > 0)
                source = sources[^1];
            if (source.Row == null)
                continue;

            var from = new Vector2(LaneX(parent), RowY(source.Row));
            var corner = new Vector2(x, from.Y);
            handle.DrawLine(from + new Vector2(Radius * s, 0), corner, BranchColor);
            handle.DrawLine(corner, new Vector2(x, top - Radius * s), BranchColor);
        }

        foreach (var (row, ev, lane) in _rows)
            DrawNode(handle, ev, new Vector2(LaneX(lane), RowY(row)), Radius * s);
    }

    private void DrawNode(DrawingHandleScreen handle, DocumentLogEventInfo ev, Vector2 pos, float r)
    {
        var square = new UIBox2(pos.X - r + 1, pos.Y - r + 1, pos.X + r - 1, pos.Y + r - 1);
        switch (ev.Type)
        {
            case DocumentEventType.Stamped:
            case DocumentEventType.Marked:
                DocumentLogStamps.DrawEvent(handle, _view, ev, pos, r);
                break;
            case DocumentEventType.Sent:
            case DocumentEventType.Copied:
                handle.DrawRect(square, PanelColor);
                handle.DrawRect(square, LightColor, false);
                break;
            case DocumentEventType.Received:
            case DocumentEventType.Printed:
                handle.DrawCircle(pos, r * 0.75f, PanelColor);
                handle.DrawCircle(pos, r * 0.75f, MutedColor, false);
                break;
            case DocumentEventType.Sealed:
                handle.DrawRect(square, MutedColor);
                break;
            case DocumentEventType.Torn:
                handle.DrawRect(square, PanelColor);
                handle.DrawRect(square, MutedColor, false);
                break;
            case DocumentEventType.Forged:
                handle.DrawCircle(pos, r, PanelColor);
                handle.DrawCircle(pos, r, RedColor, false);
                handle.DrawCircle(pos, r - 1, RedColor, false);
                break;
            case DocumentEventType.Destroyed:
                handle.DrawLine(pos + new Vector2(-r, -r), pos + new Vector2(r, r), RedColor);
                handle.DrawLine(pos + new Vector2(-r, r), pos + new Vector2(r, -r), RedColor);
                break;
        }
    }
}
