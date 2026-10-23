# Shared journal selection tooling, up29 evidence stop

Source under test: PR#3949 head3d1140bbd01d588e18655734f96da083fbf86b43. This is an evidence-tooling defect; no product failure or gameplay ending was observed. Supervisor19:30Z ruled it no budget charge and nonblocking for READY once authoritative rejoin state plus full-page journal lists prove policy. Original up29 terminal remains BLOCKED, not reclassified as a functional pass.

## Reproduction and retained evidence

A accepted Nus issue_1227 / issue_1227_quest through real dialogue. A activeLadyQuests=1, B=0. The installed shared command coop.debug.ittest.run journal_select Lady returned success with active=1 old=54, but selected an old Sora entry. The detail must not be captioned as Nus's new acceptance.

C:/Users/Andrew/CodexWindows/coop-live-orchestration/3781/up29/accept-journal-select-A-corrected.json
C:/Users/Andrew/CodexWindows/coop-live-orchestration/3781/up29/select-active-Lady-A-inspect-0.json
C:/Users/Andrew/CodexWindows/coop-live-orchestration/3781/up29/select-active-Lady-A-elements.json

Freshly observed active-row title e36 had ancestors e33/e32/e31/e26 without declared click actions. The installed operator command:
bash /home/pwisorlowska/coop-live-orchestration/3781/up29/op.sh -Mode Click -Target A -Label select-active-Lady-A -Layer QuestScreen -Match "^Lady.s Knight Out$"
returned exact error: No clickable ancestor. The row page was successfully inspected. Earlier full journal inspections separately returned stale_reference: Widget hierarchy changed; inspect again. Those are retained separately.

## Cause and minimal shared fix proposal

IssueLiveTestKit.Shared.cs:98-101 concatenates ActiveQuestsList then OldQuestsList and takes LastOrDefault by title substring. Therefore any matching old entry is preferred over a matching active one; quest name alone also cannot distinguish two personal or historical Lady entries from different givers.

Prefer exact quest/archive identity plus owner/controller identity for completed saved entries. Expose active vs old selection explicitly and report the selected quest/archive identity, giver and ownership. Resolve that exact native view-model item and invoke its existing ExecuteSelection path. Where the bridge itself is used, target the row's selectable widget/action rather than trying passive text ancestors; do not use coordinate/global-input fallback. Preserve legacy unowned history without deleting/relabeling it.

The parent prepared only an ignored issue-kit journal_active action that selects within ActiveQuestsList and uses existing ExecuteSelection, without touching shared helpers or product code. It is optional diagnostic input automation, not a prerequisite for the corrected functional-first rejoin run.

## Disposition

Route this write-up as shared tooling, per supervisor. No shared helper edits or GitHub issue/comment created here. The corrected run must finish disconnect/server cancellation/reconnect/no-resurrection and full-page Active/Old screenshots first. Optional later archive selection failure is recorded honestly and cannot erase functional proof. No synthetic quest completion or journal mutation is allowed.