using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.DocumentLog;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.DocumentLog;

/// <summary>
/// Round chronicle of stamped documents. A record is created on the first real stamp;
/// marks (approved / denied) only annotate an existing record. Records outlive the round
/// restart and are cleared when the next round starts.
/// </summary>
public sealed class DocumentLogSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private static readonly ProtoId<DocumentCategoryPrototype> OtherCategory = "Other";

    private readonly SortedDictionary<int, DocumentRecord> _records = new();
    private readonly Dictionary<string, DocumentStampPrototype> _marksByStampedName = new();
    private readonly Dictionary<(EntityUid Paper, EntityUid Actor), TimeSpan> _openedAt = new();
    private int _nextId = 1;
    private bool _recording = true;

    public IReadOnlyDictionary<int, DocumentRecord> Records => _records;
    public int RoundId => _gameTicker.RoundId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, PaperStampedEvent>(OnPaperStamped);
        SubscribeLocalEvent<PaperComponent, BoundUIOpenedEvent>(OnPaperOpened);
        SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(OnPaperClosed);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildMarkLookup();
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        Clear();
        _recording = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _recording = false;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DocumentStampPrototype>() || args.WasModified<EntityPrototype>())
            BuildMarkLookup();
    }

    public void Clear()
    {
        _records.Clear();
        _openedAt.Clear();
        _nextId = 1;
    }

    public bool TryGetRecord(EntityUid paper, [NotNullWhen(true)] out DocumentRecord? record)
    {
        record = null;
        return TryComp<DocumentRecordComponent>(paper, out var comp) && _records.TryGetValue(comp.RecordId, out record);
    }

    private void OnPaperStamped(Entity<PaperComponent> paper, ref PaperStampedEvent args)
    {
        if (!_recording || !_cfg.GetCVar(CCCVars.DocumentLogEnabled))
            return;

        var stampProto = MetaData(args.Stamp).EntityPrototype?.ID;
        if (stampProto == null || !ProtoMan.TryIndex<DocumentStampPrototype>(stampProto, out var stamp))
            return;

        var now = _gameTicker.RoundDuration();
        var actor = ResolvePerson(args.User);

        if (!TryGetRecord(paper, out var record))
        {
            if (stamp.Kind != DocumentStampKind.Stamp)
                return;

            if (_records.Count >= _cfg.GetCVar(CCCVars.DocumentLogMaxRecords))
                return;

            record = CreateRecord(paper, now, actor);
        }

        record.Events.Add(new DocumentEvent
        {
            Time = now,
            Type = stamp.Kind == DocumentStampKind.Stamp ? DocumentEventType.Stamped : DocumentEventType.Marked,
            Actor = actor,
            Stamp = stampProto,
            StampKind = stamp.Kind,
        });

        var limit = _cfg.GetCVar(CCCVars.DocumentLogMaxTextLength);
        record.Text = paper.Comp.Content.Length <= limit ? paper.Comp.Content : paper.Comp.Content[..limit];
    }

    private void OnPaperOpened(Entity<PaperComponent> paper, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is PaperComponent.PaperUiKey.Key)
            _openedAt[(paper, args.Actor)] = _gameTicker.RoundDuration();
    }

    private void OnPaperClosed(Entity<PaperComponent> paper, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not PaperComponent.PaperUiKey.Key || !_openedAt.Remove((paper, args.Actor), out var openedAt))
            return;

        if (!_recording || !TryGetRecord(paper, out var record))
            return;

        var now = _gameTicker.RoundDuration();
        if ((now - openedAt).TotalSeconds < _cfg.GetCVar(CCCVars.DocumentLogReadThreshold))
            return;

        record.Readers.Add(new DocumentReader(ResolvePerson(args.Actor), now));
    }

    private DocumentRecord CreateRecord(Entity<PaperComponent> paper, TimeSpan now, DocumentPerson author)
    {
        var record = new DocumentRecord
        {
            Id = _nextId++,
            CreatedAt = now,
            Title = Name(paper),
            Author = author,
        };
        _records.Add(record.Id, record);
        EnsureComp<DocumentRecordComponent>(paper).RecordId = record.Id;

        // Marks put on the paper before the first stamp: only their presence is known.
        foreach (var info in paper.Comp.StampedBy)
        {
            if (!_marksByStampedName.TryGetValue(info.StampedName, out var mark))
                continue;

            record.Events.Add(new DocumentEvent
            {
                Time = now,
                Type = DocumentEventType.Marked,
                Stamp = mark.ID,
                StampKind = mark.Kind,
                Retro = true,
            });
        }

        return record;
    }

    /// <summary>
    /// Snapshot of a person: name, job title (ID card first, then role) and log categories.
    /// </summary>
    public DocumentPerson ResolvePerson(EntityUid actor)
    {
        string? jobTitle = null;
        var departments = new List<ProtoId<DepartmentPrototype>>();

        if (_idCard.TryFindIdCard(actor, out var idCard))
        {
            if (!string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle))
                jobTitle = idCard.Comp.LocalizedJobTitle;
            departments.AddRange(idCard.Comp.JobDepartments);
        }

        if (_mind.TryGetMind(actor, out var mindId, out _) && _jobs.MindTryGetJob(mindId, out var job))
        {
            jobTitle ??= job.LocalizedName;
            if (departments.Count == 0 && _jobs.TryGetAllDepartments(job.ID, out var jobDepartments))
                departments.AddRange(jobDepartments.Select(d => new ProtoId<DepartmentPrototype>(d.ID)));
        }

        var categories = ResolveCategories(departments);
        return new DocumentPerson(
            Name(actor),
            jobTitle ?? Loc.GetString("documentlog-unknown-job"),
            categories[0],
            categories);
    }

    /// <summary>
    /// Departments to log categories by matching ID, heaviest first. Falls back to Other.
    /// </summary>
    private List<ProtoId<DocumentCategoryPrototype>> ResolveCategories(List<ProtoId<DepartmentPrototype>> departments)
    {
        var categories = new List<DocumentCategoryPrototype>();
        foreach (var department in departments)
        {
            if (ProtoMan.TryIndex<DocumentCategoryPrototype>(department.Id, out var category) && !categories.Contains(category))
                categories.Add(category);
        }

        if (categories.Count == 0)
            return new List<ProtoId<DocumentCategoryPrototype>> { OtherCategory };

        return categories
            .OrderByDescending(c => c.Weight)
            .Select(c => new ProtoId<DocumentCategoryPrototype>(c.ID))
            .ToList();
    }

    /// <summary>
    /// Maps the display name of every mark stamp to its prototype, so marks already on a paper
    /// can be recognised from <see cref="StampDisplayInfo"/> alone.
    /// </summary>
    private void BuildMarkLookup()
    {
        _marksByStampedName.Clear();
        foreach (var stamp in ProtoMan.EnumeratePrototypes<DocumentStampPrototype>())
        {
            if (stamp.Kind == DocumentStampKind.Stamp)
                continue;

            if (ProtoMan.TryIndex<EntityPrototype>(stamp.ID, out var entity)
                && entity.TryGetComponent<StampComponent>(out var comp, _factory))
                _marksByStampedName[comp.StampedName] = stamp;
        }
    }
}
