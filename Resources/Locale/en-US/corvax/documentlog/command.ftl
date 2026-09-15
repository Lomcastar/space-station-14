cmd-documentlog-desc = Prints the document log of the current or just-finished round.
cmd-documentlog-help = Usage: documentlog [id]

documentlog-command-disabled = Document log is disabled (documentlog.enabled).
documentlog-command-empty = Document log is empty.
documentlog-command-not-found = Document #{ $id } not found.
documentlog-command-bad-id = Document id must be an integer.
documentlog-command-header = Document log, round { $round }: { $count } record(s).
documentlog-command-record = #{ $id }  { $time }  "{ $title }" — { $author }
documentlog-command-person = { $name } ({ $job }, { $category })
documentlog-command-event-stamped = { $time }  stamp: { $stamp } [{ $category }] — { $actor }
documentlog-command-event-marked = { $time }  mark: { $mark } — { $actor }
documentlog-command-event-marked-retro = (before registration)  mark: { $mark }
documentlog-command-readers = readers ({ $count }): { $readers }
documentlog-command-mark-approve = APPROVED
documentlog-command-mark-deny = DENIED
