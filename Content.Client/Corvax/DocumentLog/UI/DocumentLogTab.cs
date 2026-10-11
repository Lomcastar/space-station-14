using System.Linq;
using System.Numerics;
using Content.Shared.Corvax.DocumentLog;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Corvax.DocumentLog.UI;

/// <summary>
/// Round-end tab listing every registered document of the shift.
/// </summary>
public sealed class DocumentLogTab : BoxContainer
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly Color HeaderColor = Color.FromHex("#686C7C");

    private readonly DocumentLogView _view;
    private readonly DocumentLogSystem _system;
    private readonly BoxContainer _rows = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly Dictionary<int, DocumentLogDetailsEvent> _details = new();

    public DocumentLogTab(DocumentLogSnapshotEvent snapshot)
    {
        IoCManager.InjectDependencies(this);
        _system = _entMan.System<DocumentLogSystem>();
        _view = new DocumentLogView(snapshot);
        if (_player.LocalEntity is { } local)
            _view.LocalName = _entMan.GetComponent<MetaDataComponent>(local).EntityName;

        Orientation = LayoutOrientation.Vertical;
        Name = Loc.GetString("document-log-tab-title");

        AddChild(MakeToolbar());
        AddChild(new DocumentLogAxis(_view) { Margin = new Thickness(10, 4, 10, 6) });
        AddChild(MakeColumnHeader());

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_rows);
        AddChild(scroll);

        _view.Changed += Rebuild;
        _system.DetailsReceived += OnDetails;
        Rebuild();
    }

    private Control MakeToolbar()
    {
        var toolbar = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(10, 10, 10, 2) };

        var top = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var search = new LineEdit
        {
            PlaceHolder = Loc.GetString("document-log-search-placeholder"),
            HorizontalExpand = true,
            MinSize = new Vector2(160, 1),
        };
        search.OnTextChanged += args =>
        {
            _view.Search = args.Text.Trim();
            _view.NotifyChanged();
        };
        top.AddChild(search);

        var mine = new CheckBox { Text = Loc.GetString("document-log-mine") };
        mine.OnToggled += args =>
        {
            _view.MineOnly = args.Pressed;
            _view.NotifyChanged();
        };
        top.AddChild(mine);
        toolbar.AddChild(top);

        var chips = new WrapContainer { Margin = new Thickness(0, 6, 0, 0), SeparationOverride = 4, CrossSeparationOverride = 4 };
        var present = _view.Roots.SelectMany(r => _view.Family(r)).SelectMany(f => _view.CategoriesOf(f.Record)).ToHashSet();
        var categories = _proto.EnumeratePrototypes<DocumentCategoryPrototype>()
            .Where(c => present.Contains(c.ID))
            .OrderByDescending(c => c.Weight)
            .ThenBy(c => c.ID);
        foreach (var category in categories)
        {
            var button = new Button { Text = Loc.GetString(category.Name), ToggleMode = true };
            button.OnToggled += args =>
            {
                button.Modulate = args.Pressed ? category.Color : Color.White;
                if (args.Pressed)
                    _view.Categories.Add(category.ID);
                else
                    _view.Categories.Remove(category.ID);
                _view.NotifyChanged();
            };
            chips.AddChild(button);
        }
        toolbar.AddChild(chips);

        return toolbar;
    }

    private Control MakeColumnHeader()
    {
        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(10, 0, 10, 2), SeparationOverride = DocumentLogRow.Separation };
        header.AddChild(HeaderLabel(null, DocumentLogRow.ChevronWidth));
        header.AddChild(HeaderLabel("document-log-column-time", DocumentLogRow.TimeWidth));
        header.AddChild(StretchLabel("document-log-column-document", DocumentLogRow.TitleRatio));
        header.AddChild(HeaderLabel("document-log-column-stamps", 79));
        header.AddChild(StretchLabel("document-log-column-author", DocumentLogRow.AuthorRatio));
        header.AddChild(HeaderLabel(null, DocumentLogRow.CountsWidth));
        var state = HeaderLabel("document-log-column-state", DocumentLogRow.StateWidth);
        state.Align = Label.AlignMode.Right;
        header.AddChild(state);
        return header;
    }

    private static Label StretchLabel(string key, float ratio)
    {
        return new Label
        {
            Text = Loc.GetString(key),
            HorizontalExpand = true,
            SizeFlagsStretchRatio = ratio,
            StyleClasses = { "LabelSmall" },
            FontColorOverride = HeaderColor,
        };
    }

    private static Label HeaderLabel(string? key, float width)
    {
        return new Label
        {
            Text = key == null ? string.Empty : Loc.GetString(key),
            MinWidth = width,
            MaxWidth = width,
            StyleClasses = { "LabelSmall" },
            FontColorOverride = HeaderColor,
        };
    }

    private void Rebuild()
    {
        var open = _rows.Children.OfType<DocumentLogRow>().Where(r => r.Expanded).Select(r => r.Root.Id).ToHashSet();
        _rows.RemoveAllChildren();

        foreach (var root in _view.VisibleRoots())
        {
            var row = new DocumentLogRow(_view, root, RequestDetails);
            foreach (var details in _details.Values.Where(d => row.Contains(d.RecordId)))
                row.SetDetails(details);
            if (open.Contains(root.Id))
                row.Expand();
            _rows.AddChild(row);
        }

        if (_rows.ChildCount == 0)
            _rows.AddChild(new Label { Text = Loc.GetString("document-log-empty"), Margin = new Thickness(10, 12), FontColorOverride = HeaderColor });
    }

    private void RequestDetails(int recordId)
    {
        if (!_details.ContainsKey(recordId))
            _system.RequestDetails(recordId);
    }

    private void OnDetails(DocumentLogDetailsEvent details)
    {
        _details[details.RecordId] = details;
        foreach (var row in _rows.Children.OfType<DocumentLogRow>())
        {
            if (row.Contains(details.RecordId))
                row.SetDetails(details);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _system.DetailsReceived -= OnDetails;
    }
}
