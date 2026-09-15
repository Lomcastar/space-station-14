using System.Text;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.DocumentLog;
using Content.Shared.Paper;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.DocumentLog;

[AdminCommand(AdminFlags.Logs)]
public sealed class DocumentLogCommand : LocalizedEntityCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private DocumentLogSystem _log = default!;

    public override string Command => "documentlog";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_cfg.GetCVar(CCCVars.DocumentLogEnabled))
        {
            shell.WriteLine(Loc.GetString("documentlog-command-disabled"));
            return;
        }

        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out var id))
            {
                shell.WriteError(Loc.GetString("documentlog-command-bad-id"));
                return;
            }

            if (!_log.Records.TryGetValue(id, out var record))
            {
                shell.WriteError(Loc.GetString("documentlog-command-not-found", ("id", id)));
                return;
            }

            shell.WriteLine(FormatRecord(record));
            return;
        }

        if (_log.Records.Count == 0)
        {
            shell.WriteLine(Loc.GetString("documentlog-command-empty"));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("documentlog-command-header", ("round", _log.RoundId), ("count", _log.Records.Count)));
        foreach (var record in _log.Records.Values)
        {
            sb.AppendLine(FormatRecord(record));
        }

        shell.WriteLine(sb.ToString().TrimEnd());
    }

    private string FormatRecord(DocumentRecord record)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("documentlog-command-record",
            ("id", record.Id),
            ("time", FormatTime(record.CreatedAt)),
            ("title", record.Title),
            ("author", FormatPerson(record.Author))));

        foreach (var ev in record.Events)
        {
            sb.Append("    ");
            sb.AppendLine(FormatEvent(ev));
        }

        return sb.ToString().TrimEnd();
    }

    private string FormatEvent(DocumentEvent ev)
    {
        switch (ev.Type)
        {
            case DocumentEventType.Stamped:
                return Loc.GetString("documentlog-command-event-stamped",
                    ("time", FormatTime(ev.Time)),
                    ("stamp", StampName(ev.Stamp)),
                    ("category", StampCategory(ev.Stamp)),
                    ("actor", FormatPerson(ev.Actor)));

            case DocumentEventType.Marked:
                var mark = Loc.GetString(ev.StampKind == DocumentStampKind.Approve
                    ? "documentlog-command-mark-approve"
                    : "documentlog-command-mark-deny");
                if (ev.Retro)
                    return Loc.GetString("documentlog-command-event-marked-retro", ("mark", mark));
                return Loc.GetString("documentlog-command-event-marked",
                    ("time", FormatTime(ev.Time)),
                    ("mark", mark),
                    ("actor", FormatPerson(ev.Actor)));

            default:
                return ev.Type.ToString();
        }
    }

    private string FormatPerson(DocumentPerson? person)
    {
        if (person == null)
            return "?";

        return Loc.GetString("documentlog-command-person",
            ("name", person.Name),
            ("job", person.JobTitle),
            ("category", CategoryName(person.Category)));
    }

    private string CategoryName(ProtoId<DocumentCategoryPrototype> id)
    {
        return _proto.TryIndex(id, out var category) ? Loc.GetString(category.Name) : id.Id;
    }

    private string StampName(EntProtoId? stamp)
    {
        if (stamp == null || !_proto.TryIndex<EntityPrototype>(stamp.Value, out var entity))
            return "?";

        return entity.TryGetComponent<StampComponent>(out var comp, _factory)
            ? Loc.GetString(comp.StampedName)
            : entity.Name;
    }

    private string StampCategory(EntProtoId? stamp)
    {
        if (stamp == null || !_proto.TryIndex<DocumentStampPrototype>(stamp.Value, out var proto) || proto.Categories.Count == 0)
            return "-";

        return CategoryName(proto.Categories[0]);
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.ToString(@"hh\:mm\:ss");
    }
}
