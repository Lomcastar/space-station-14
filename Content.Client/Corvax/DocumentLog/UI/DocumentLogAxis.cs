using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.Corvax.DocumentLog.UI;

/// <summary>
/// Time axis above the rows: tick marks, a histogram of registrations, and drag-to-select a time window
/// that narrows the list below.
/// </summary>
public sealed class DocumentLogAxis : Control
{
    private const int Buckets = 60;
    private const float HistogramHeight = 14f;
    private const float MinDrag = 4f;

    private static readonly Color TickColor = Color.FromHex("#3D4854");
    private static readonly Color TextColor = Color.FromHex("#9AA5AF");
    private static readonly Color BarColor = Color.FromHex("#6B7680");
    private static readonly Color WindowColor = Color.FromHex("#4FBF7A").WithAlpha(0.18f);

    private readonly DocumentLogView _view;
    private readonly Font _font;
    private readonly int[] _histogram = new int[Buckets];
    private int _maxBucket;

    private float? _dragStart;
    private float _dragCurrent;

    public DocumentLogAxis(DocumentLogView view)
    {
        _view = view;
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
        MouseFilter = MouseFilterMode.Stop;
        MinHeight = 34;
        HorizontalExpand = true;

        foreach (var record in view.Snapshot.Records)
        {
            var bucket = Math.Min(Buckets - 1, (int) (view.Fraction(record.CreatedAt) * Buckets));
            _histogram[bucket]++;
        }
        _maxBucket = Math.Max(1, _histogram.Max());
    }

    private float X(TimeSpan time) => _view.Fraction(time) * PixelWidth;

    private TimeSpan TimeAt(float x) => _view.Snapshot.RoundDuration * Math.Clamp(x / PixelWidth, 0f, 1f);

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;

        var bucketWidth = width / (float) Buckets;
        for (var i = 0; i < Buckets; i++)
        {
            if (_histogram[i] == 0)
                continue;
            var barHeight = HistogramHeight * _histogram[i] / _maxBucket;
            handle.DrawRect(new UIBox2(i * bucketWidth + 1, HistogramHeight - barHeight, (i + 1) * bucketWidth - 1, HistogramHeight), BarColor);
        }

        handle.DrawLine(new Vector2(0, HistogramHeight + 2), new Vector2(width, HistogramHeight + 2), TickColor);

        var step = TickStep(_view.Snapshot.RoundDuration);
        for (var t = TimeSpan.Zero; t <= _view.Snapshot.RoundDuration; t += step)
        {
            var x = X(t);
            handle.DrawLine(new Vector2(x, HistogramHeight + 2), new Vector2(x, height), TickColor);
            handle.DrawString(_font, new Vector2(x + 3, HistogramHeight + 4), t.ToString(@"hh\:mm"), TextColor);
        }

        if (_dragStart is { } start)
        {
            var (a, b) = (Math.Min(start, _dragCurrent), Math.Max(start, _dragCurrent));
            handle.DrawRect(new UIBox2(a, 0, b, height), WindowColor);
        }
        else if (!_view.WindowIsFull)
        {
            handle.DrawRect(new UIBox2(X(_view.WindowStart), 0, X(_view.WindowEnd), height), WindowColor);
        }
    }

    private static TimeSpan TickStep(TimeSpan duration)
    {
        if (duration <= TimeSpan.FromMinutes(5))
            return TimeSpan.FromMinutes(1);
        if (duration <= TimeSpan.FromMinutes(15))
            return TimeSpan.FromMinutes(2);
        if (duration <= TimeSpan.FromMinutes(30))
            return TimeSpan.FromMinutes(5);
        if (duration <= TimeSpan.FromHours(1))
            return TimeSpan.FromMinutes(10);
        if (duration <= TimeSpan.FromHours(2))
            return TimeSpan.FromMinutes(15);
        return TimeSpan.FromMinutes(30);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragStart = args.RelativePixelPosition.X;
        _dragCurrent = _dragStart.Value;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragStart != null)
            _dragCurrent = args.RelativePixelPosition.X;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || _dragStart is not { } start)
            return;

        _dragStart = null;
        var end = args.RelativePixelPosition.X;
        if (Math.Abs(end - start) < MinDrag)
        {
            _view.WindowStart = TimeSpan.Zero;
            _view.WindowEnd = _view.Snapshot.RoundDuration;
        }
        else
        {
            _view.WindowStart = TimeAt(Math.Min(start, end));
            _view.WindowEnd = TimeAt(Math.Max(start, end));
        }

        _view.NotifyChanged();
        args.Handle();
    }
}
