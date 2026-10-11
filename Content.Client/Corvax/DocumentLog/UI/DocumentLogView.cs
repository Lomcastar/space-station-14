using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Corvax.DocumentLog;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.DocumentLog.UI;

/// <summary>
/// Snapshot grouped into document families (an original and all its copies) plus the current filters.
/// </summary>
public sealed class DocumentLogView
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;

    public readonly DocumentLogSnapshotEvent Snapshot;
    public readonly List<DocumentLogRecordInfo> Roots = new();

    private readonly Dictionary<int, List<DocumentLogRecordInfo>> _children = new();

    public TimeSpan WindowStart;
    public TimeSpan WindowEnd;
    public string Search = string.Empty;
    public bool MineOnly;
    public readonly HashSet<string> Categories = new();
    public string? LocalName;

    public event Action? Changed;

    public DocumentLogView(DocumentLogSnapshotEvent snapshot)
    {
        IoCManager.InjectDependencies(this);
        Snapshot = snapshot;
        WindowEnd = snapshot.RoundDuration;

        var ids = snapshot.Records.Select(r => r.Id).ToHashSet();
        foreach (var record in snapshot.Records.OrderBy(r => r.CreatedAt))
        {
            if (record.ParentId is { } parent && ids.Contains(parent))
                _children.GetOrNew(parent).Add(record);
            else
                Roots.Add(record);
        }
    }

    public bool WindowIsFull => WindowStart <= TimeSpan.Zero && WindowEnd >= Snapshot.RoundDuration;

    public void NotifyChanged() => Changed?.Invoke();

    public float Fraction(TimeSpan time)
    {
        if (Snapshot.RoundDuration <= TimeSpan.Zero)
            return 0f;
        return Math.Clamp((float) (time / Snapshot.RoundDuration), 0f, 1f);
    }

    /// <summary>
    /// The root followed by its copies, depth-first, with each record's depth.
    /// </summary>
    public List<(DocumentLogRecordInfo Record, int Depth)> Family(DocumentLogRecordInfo root)
    {
        var result = new List<(DocumentLogRecordInfo, int)>();
        Walk(root, 0);
        return result;

        void Walk(DocumentLogRecordInfo record, int depth)
        {
            result.Add((record, depth));
            if (!_children.TryGetValue(record.Id, out var children))
                return;
            foreach (var child in children)
                Walk(child, depth + 1);
        }
    }

    public IEnumerable<DocumentLogRecordInfo> VisibleRoots() => Roots.Where(Matches);

    private bool Matches(DocumentLogRecordInfo root)
    {
        var family = Family(root).Select(f => f.Record).ToList();
        var events = family.SelectMany(r => r.Events).ToList();

        if (!WindowIsFull && events.Count > 0
            && (events.Min(e => e.Time) > WindowEnd || events.Max(e => e.Time) < WindowStart))
            return false;

        var people = family.SelectMany(People).ToList();
        if (MineOnly && LocalName != null && people.All(p => p.Name != LocalName))
            return false;

        if (Categories.Count > 0 && !family.SelectMany(CategoriesOf).Any(Categories.Contains))
            return false;

        if (Search.Length > 0
            && !root.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)
            && !people.Any(p => p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static IEnumerable<DocumentLogPersonInfo> People(DocumentLogRecordInfo record)
    {
        yield return record.Author;
        foreach (var ev in record.Events)
        {
            if (ev.Actor != null)
                yield return ev.Actor;
        }
    }

    public IEnumerable<string> CategoriesOf(DocumentLogRecordInfo record)
    {
        foreach (var person in People(record))
        {
            foreach (var category in person.Categories)
                yield return category;
        }

        foreach (var ev in record.Events)
        {
            if (ev.Stamp != null && _proto.TryIndex<DocumentStampPrototype>(ev.Stamp, out var stamp))
            {
                foreach (var category in stamp.Categories)
                    yield return category.Id;
            }
        }
    }

    public bool IsMark(DocumentLogEventInfo ev, DocumentStampKind kind)
    {
        return ev.Type == DocumentEventType.Marked && ev.StampKind == kind;
    }

    public Color StampColor(string? stamp)
    {
        return TryStamp(stamp, out var comp) ? comp.StampedColor : Color.Gray;
    }

    public string StampName(string? stamp)
    {
        return TryStamp(stamp, out var comp) ? Loc.GetString(comp.StampedName) : "?";
    }

    private bool TryStamp(string? stamp, [NotNullWhen(true)] out StampComponent? comp)
    {
        comp = null;
        return stamp != null
            && _proto.TryIndex<EntityPrototype>(stamp, out var entity)
            && entity.TryGetComponent(out comp, _factory);
    }

    public static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm\:ss");

    public static string FormatPerson(DocumentLogPersonInfo person)
    {
        return Loc.GetString("document-log-person", ("name", person.Name), ("job", person.JobTitle));
    }

    public string Describe(DocumentLogEventInfo ev)
    {
        return ev.Type switch
        {
            DocumentEventType.Stamped => StampName(ev.Stamp),
            DocumentEventType.Marked when ev.Retro => Loc.GetString("document-log-event-marked-retro", ("mark", StampName(ev.Stamp))),
            DocumentEventType.Marked => StampName(ev.Stamp),
            DocumentEventType.Forged => Loc.GetString("document-log-event-forged"),
            DocumentEventType.Sealed => Loc.GetString("document-log-event-sealed"),
            DocumentEventType.Torn => Loc.GetString("document-log-event-torn"),
            DocumentEventType.Sent => Loc.GetString("document-log-event-sent", ("to", ev.FaxTo ?? "?")),
            DocumentEventType.Copied => Loc.GetString("document-log-event-copied"),
            DocumentEventType.Received => Loc.GetString("document-log-event-received", ("from", ev.FaxFrom ?? "?")),
            DocumentEventType.Printed => Loc.GetString("document-log-event-printed", ("fax", ev.FaxFrom ?? "?")),
            DocumentEventType.Destroyed => Loc.GetString("document-log-event-destroyed"),
            _ => ev.Type.ToString(),
        };
    }

    public static string FateName(DocumentFate fate)
    {
        return Loc.GetString($"documentlog-fate-{fate.ToString().ToLowerInvariant()}");
    }
}
