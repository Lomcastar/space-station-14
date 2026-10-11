using System.Linq;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.DocumentLog;
using Content.Shared.GameTicking;
using Robust.Shared.Player;

namespace Content.Server.Corvax.DocumentLog;

public sealed partial class DocumentLogSystem
{
    private void InitializeNetwork()
    {
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
        SubscribeNetworkEvent<DocumentLogDetailsRequestEvent>(OnDetailsRequest);
    }

    private void OnRoundEnded(RoundEndedEvent ev)
    {
        if (!_cfg.GetCVar(CCCVars.DocumentLogEnabled))
            return;

        RaiseNetworkEvent(new DocumentLogSnapshotEvent
        {
            RoundId = ev.RoundId,
            RoundDuration = ev.RoundDuration,
            Records = _records.Values.Select(ToInfo).ToList(),
        });
    }

    private void OnDetailsRequest(DocumentLogDetailsRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_records.TryGetValue(ev.RecordId, out var record))
            return;

        RaiseNetworkEvent(new DocumentLogDetailsEvent
        {
            RecordId = record.Id,
            Text = record.Text,
            Stamps = record.Stamps,
            Readers = record.Readers.Select(r => ToInfo(r.Person)).ToList(),
        }, args.SenderSession);
    }

    private static DocumentLogRecordInfo ToInfo(DocumentRecord record)
    {
        return new DocumentLogRecordInfo
        {
            Id = record.Id,
            CreatedAt = record.CreatedAt,
            Title = record.Title,
            Author = ToInfo(record.Author),
            ParentId = record.ParentId,
            Events = record.Events.Select(ToInfo).ToList(),
            ReaderCount = record.Readers.Count,
            Fate = record.Fate,
        };
    }

    private static DocumentLogEventInfo ToInfo(DocumentEvent ev)
    {
        return new DocumentLogEventInfo
        {
            Time = ev.Time,
            Type = ev.Type,
            Actor = ev.Actor == null ? null : ToInfo(ev.Actor),
            Stamp = ev.Stamp?.Id,
            StampKind = ev.StampKind,
            Retro = ev.Retro,
            FaxFrom = ev.FaxFrom,
            FaxTo = ev.FaxTo,
        };
    }

    private static DocumentLogPersonInfo ToInfo(DocumentPerson person)
    {
        return new DocumentLogPersonInfo
        {
            Name = person.Name,
            JobTitle = person.JobTitle,
            Category = person.Category.Id,
            Categories = person.Categories.Select(c => c.Id).ToList(),
        };
    }
}
