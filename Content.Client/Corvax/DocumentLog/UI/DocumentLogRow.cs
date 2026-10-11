using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.Corvax.DocumentLog;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Corvax.DocumentLog.UI;

/// <summary>
/// One document family: a summary line that expands into the chain, readers, copies and text.
/// </summary>
public sealed class DocumentLogRow : BoxContainer
{
    public const float ChevronWidth = 12f;
    public const float TimeWidth = 76f;
    public const float CountsWidth = 96f;
    public const float StateWidth = 80f;
    public const float TitleRatio = 3f;
    public const float AuthorRatio = 2f;
    public const int Separation = 10;

    private static readonly Color MutedColor = Color.FromHex("#8E93A3");
    private static readonly Color HeadingColor = Color.FromHex("#686C7C");
    private static readonly Color HoverColor = Color.FromHex("#363846");
    private static readonly Color OpenColor = Color.FromHex("#3B3D4E");
    private static readonly Color DetailsColor = Color.FromHex("#30313E");
    private static readonly Color SeparatorColor = Color.FromHex("#3D3F4D");
    private static readonly Color AccentColor = Color.FromHex("#D1A85A");
    private static readonly Color RedColor = Color.FromHex("#E0645D");
    private static readonly Color GreenColor = Color.FromHex("#5FC382");
    private static readonly Color AmberColor = Color.FromHex("#E3B450");
    private static readonly Color PaperColor = Color.FromHex("#E9E2CF");

    public readonly DocumentLogRecordInfo Root;
    public readonly List<(DocumentLogRecordInfo Record, int Depth)> Family;

    private readonly DocumentLogView _view;
    private readonly Action<int> _requestDetails;
    private readonly StyleBoxFlat _headerStyle = new();
    private readonly Chevron _chevron = new();
    private readonly PanelContainer _details;
    private readonly BoxContainer _readers;
    private readonly Label _textHeading;
    private readonly RichTextLabel _text;
    private readonly Label _noText;
    private int _shownText;
    private readonly Dictionary<int, DocumentLogDetailsEvent> _received = new();
    private bool _requested;
    private bool _hovered;

    public DocumentLogRow(DocumentLogView view, DocumentLogRecordInfo root, Action<int> requestDetails)
    {
        _view = view;
        Root = root;
        Family = view.Family(root);
        _requestDetails = requestDetails;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        var header = new PanelContainer
        {
            PanelOverride = _headerStyle,
            MouseFilter = MouseFilterMode.Stop,
            DefaultCursorShape = CursorShape.Hand,
        };
        header.OnMouseEntered += _ => { _hovered = true; UpdateStyle(); };
        header.OnMouseExited += _ => { _hovered = false; UpdateStyle(); };
        header.OnKeyBindDown += OnHeaderClick;
        header.AddChild(MakeSummary());
        AddChild(header);

        var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(12, 6, 10, 12) };

        body.AddChild(new DocumentLogChain(view, Family));

        var facts = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), SeparationOverride = 16 };
        var left = new BoxContainer { Orientation = LayoutOrientation.Vertical, MinWidth = 200, MaxWidth = 200 };
        left.AddChild(Heading("document-log-readers-heading"));
        _readers = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        _readers.AddChild(new Label { Text = Loc.GetString("document-log-loading"), FontColorOverride = MutedColor });
        left.AddChild(_readers);
        facts.AddChild(left);

        var right = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        _textHeading = Heading("document-log-text-heading", ("id", root.Id));
        right.AddChild(_textHeading);
        if (Family.Count > 1)
        {
            var versions = new WrapContainer { SeparationOverride = 4, CrossSeparationOverride = 4, Margin = new Thickness(0, 0, 0, 4) };
            var group = new ButtonGroup(false);
            foreach (var (record, _) in Family)
            {
                var id = record.Id;
                var button = new Button
                {
                    Text = Loc.GetString("document-log-lane", ("id", id)),
                    ToggleMode = true,
                    Group = group,
                    Pressed = id == root.Id,
                    StyleClasses = { "OpenBoth" },
                };
                button.OnPressed += _ => ShowText(id);
                versions.AddChild(button);
            }
            right.AddChild(versions);
        }

        var paper = new PanelContainer { PanelOverride = new StyleBoxFlat(PaperColor), HorizontalExpand = true };
        var paperContent = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(10, 8) };
        _text = new RichTextLabel { HorizontalExpand = true, Visible = false };
        _noText = new Label { Text = Loc.GetString("document-log-loading"), FontColorOverride = Color.FromHex("#6E675A") };
        paperContent.AddChild(_text);
        paperContent.AddChild(_noText);
        paper.AddChild(paperContent);
        right.AddChild(paper);
        _shownText = root.Id;
        facts.AddChild(right);
        body.AddChild(facts);

        _details = new PanelContainer
        {
            Visible = false,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = DetailsColor,
                BorderColor = AccentColor,
                BorderThickness = new Thickness(3, 0, 0, 0),
            },
        };
        _details.AddChild(body);
        AddChild(_details);

        UpdateStyle();
    }

    private Control MakeSummary()
    {
        var line = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(10, 6), SeparationOverride = Separation };

        line.AddChild(_chevron);
        line.AddChild(new Label { Text = DocumentLogView.FormatTime(Root.CreatedAt), MinWidth = TimeWidth, MaxWidth = TimeWidth, StyleClasses = { "monospace" }, FontColorOverride = MutedColor });
        line.AddChild(new Label
        {
            Text = Loc.GetString("document-log-row-title", ("id", Root.Id), ("title", Root.Title)),
            HorizontalExpand = true,
            SizeFlagsStretchRatio = TitleRatio,
            ClipText = true,
        });

        var stamps = Family
            .SelectMany(f => f.Record.Events)
            .Where(e => e.Type is DocumentEventType.Stamped or DocumentEventType.Marked)
            .OrderBy(e => e.Time)
            .ToList();
        line.AddChild(new DocumentLogStamps(_view, stamps) { ToolTip = StampsTooltip(stamps) });

        line.AddChild(new Label
        {
            Text = DocumentLogView.FormatPerson(Root.Author),
            HorizontalExpand = true,
            SizeFlagsStretchRatio = AuthorRatio,
            ClipText = true,
            ToolTip = DocumentLogView.FormatPerson(Root.Author),
        });
        line.AddChild(new Label
        {
            Text = Counts(),
            MinWidth = CountsWidth,
            MaxWidth = CountsWidth,
            ClipText = true,
            StyleClasses = { "LabelSmall" },
            FontColorOverride = MutedColor,
        });

        var (state, color) = State();
        line.AddChild(new Label { Text = state, MinWidth = StateWidth, MaxWidth = StateWidth, Align = Label.AlignMode.Right, ClipText = true, FontColorOverride = color });
        return line;
    }

    private string StampsTooltip(List<DocumentLogEventInfo> stamps)
    {
        return string.Join("\n", stamps.Select(e => e.Actor == null
            ? _view.Describe(e)
            : $"{_view.Describe(e)} — {DocumentLogView.FormatPerson(e.Actor)}"));
    }

    private string Counts()
    {
        var parts = new List<string>();
        if (Family.Count > 1)
            parts.Add(Loc.GetString("document-log-count-copies", ("count", Family.Count - 1)));
        var readers = Family.Sum(f => f.Record.ReaderCount);
        if (readers > 0)
            parts.Add(Loc.GetString("document-log-count-readers", ("count", readers)));
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The one coloured word of the row: what happened to the document in the end.
    /// </summary>
    private (string, Color) State()
    {
        if (Root.Fate is DocumentFate.Burned or DocumentFate.Disposed or DocumentFate.Destroyed)
            return (DocumentLogView.FateName(Root.Fate), RedColor);

        var centcom = Family.FirstOrDefault(f => f.Record.Fate is DocumentFate.EvacuatedToCentcom or DocumentFate.SentToCentcom);
        if (centcom.Record != null)
            return (DocumentLogView.FateName(centcom.Record.Fate), GreenColor);

        if (Family.Any(f => f.Record.Events.Any(e => e.Type == DocumentEventType.Forged)))
            return (Loc.GetString("document-log-state-forged"), AmberColor);

        return (string.Empty, MutedColor);
    }

    private static Label Heading(string key, params (string, object)[] args)
    {
        return new Label
        {
            Text = Loc.GetString(key, args),
            StyleClasses = { "LabelSmall" },
            FontColorOverride = HeadingColor,
            Margin = new Thickness(0, 4, 0, 2),
        };
    }

    private void UpdateStyle()
    {
        var open = _details.Visible;
        _headerStyle.BackgroundColor = open ? OpenColor : _hovered ? HoverColor : Color.Transparent;
        _headerStyle.BorderColor = open ? AccentColor : SeparatorColor;
        _headerStyle.BorderThickness = open ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 0, 1);
        _chevron.Open = open;
    }

    public bool Expanded => _details.Visible;

    public void Expand()
    {
        if (!_details.Visible)
            Toggle();
    }

    private void OnHeaderClick(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Toggle();
        args.Handle();
    }

    private void Toggle()
    {
        _details.Visible = !_details.Visible;
        UpdateStyle();
        if (!_details.Visible || _requested)
            return;

        _requested = true;
        foreach (var (record, _) in Family)
            _requestDetails(record.Id);
    }

    public bool Contains(int recordId) => Family.Any(f => f.Record.Id == recordId);

    public void SetDetails(DocumentLogDetailsEvent details)
    {
        _received[details.RecordId] = details;

        var readers = _received.Values
            .SelectMany(d => d.Readers)
            .GroupBy(p => p.Name)
            .Select(g => DocumentLogView.FormatPerson(g.First()))
            .ToList();
        _readers.RemoveAllChildren();
        if (readers.Count == 0)
            _readers.AddChild(new Label { Text = Loc.GetString("document-log-no-readers"), FontColorOverride = MutedColor });
        foreach (var reader in readers)
            _readers.AddChild(new Label { Text = reader, ToolTip = reader, ClipText = true, FontColorOverride = MutedColor });

        if (details.RecordId == _shownText)
            ShowText(_shownText);
    }

    private void ShowText(int recordId)
    {
        _shownText = recordId;
        _textHeading.Text = Loc.GetString("document-log-text-heading", ("id", recordId));

        if (!_received.TryGetValue(recordId, out var details))
        {
            _text.Visible = false;
            _noText.Text = Loc.GetString("document-log-loading");
            _noText.Visible = true;
            return;
        }

        var empty = string.IsNullOrWhiteSpace(details.Text);
        _text.Visible = !empty;
        _noText.Text = Loc.GetString("document-log-no-text");
        _noText.Visible = empty;
        if (!empty)
            _text.SetMarkupPermissive($"[font size=10][color=#2B2620]{details.Text}[/color][/font]");
    }

    private sealed class Chevron : Control
    {
        public bool Open;

        public Chevron()
        {
            MinSize = new Vector2(ChevronWidth, 12);
            MaxWidth = ChevronWidth;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var s = UIScale;
            var c = new Vector2(PixelWidth / 2f, PixelHeight / 2f);
            if (Open)
            {
                handle.DrawLine(c + new Vector2(-4, -2) * s, c + new Vector2(0, 2) * s, AccentColor);
                handle.DrawLine(c + new Vector2(0, 2) * s, c + new Vector2(4, -2) * s, AccentColor);
            }
            else
            {
                handle.DrawLine(c + new Vector2(-2, -4) * s, c + new Vector2(2, 0) * s, MutedColor);
                handle.DrawLine(c + new Vector2(2, 0) * s, c + new Vector2(-2, 4) * s, MutedColor);
            }
        }
    }
}
