# Quests and issues in co-op

Vanilla issues and quests assume one player. This folder is the shared code that lets several players
use them in one campaign. Only quest types that have been wired up here run. Every other vanilla issue
stays switched off.

## What players get

- Every player sees the same issue on the same notable or lord, built from the server's values.
- The first player to accept an issue owns it. Everyone else sees it as taken and nothing more: no quest,
  no journal entry, no pop-up.
- Rewards, relations and endings are worked out by the server as the owning player. Clients only show the
  result.
- Quests are not saved between sessions. A quest ends when its player leaves or the server restarts.

## Life of an issue

| Step | What happens |
| --- | --- |
| Created | Only the server creates issues. It records what the constructor rolled (random picks, hideouts) and sends it, and every client builds the same issue from that. |
| Accepted, do it yourself | The player's client starts the quest and asks the server. The first request wins. The server runs the accept as that player, keeps a copy of the quest that never runs (the mirror), and tells everyone the issue is taken. A losing player's quest is cancelled. |
| Accepted, send troops | The party screen takes the troops and the client asks the server. The server checks them, claims the issue and runs vanilla's start once. Only the sender runs the start themselves and gets the pop-up and journal entry. Everyone else only learns the issue is taken. |
| Ended | The owner's client asks the server to end it, or to run one outcome branch. When something the server sees ends the quest (a war, a clan changing kingdom, the quest giver being captured), the server runs the quest's own vanilla listener as the owner and ends it without being asked. Either way the server runs the real vanilla ending as the owner and announces it. Every client closes its copy without running the ending again. A client never ends a quest on its own while it is replaying something the server sent. It waits for the server's ending. |
| Player leaves | Their quest is ended for everyone. Troops they sent keep going and come back on the server's daily tick. |
| Server starts | Every quest nobody owns is ended. |
| Player joins late | They download the server's save and are also sent who owns what, so they do not see other players' missions. |

## Adding a quest type

Make a folder `Quests/<QuestName>/`, named after the vanilla issue class without `IssueBehavior`, and
add these files. `GangLeaderNeedsToOffloadStolenGoods` is the worked example. `ArmyNeedsSupplies` shows the
newer shape for branch patches (step 4), and `LordNeedsHorses` shows the client patches in step 7.

1. `<QuestName>CreationCaptureStrategy.cs` (`ICreationCaptureStrategy`). Reads what the constructor rolled
   and rebuilds the issue on clients. `TryBuildDebugCapture` is needed by the `give` command and must
   refuse any owner the issue would throw on, and any owner the quest type cannot have. The `give` command
   only refuses player heroes, so a notable-only quest checks for a notable here.
2. `<QuestName>QuestSolutionAcceptStrategy.cs` (`IQuestSolutionAcceptStrategy`). Carries anything the
   accept rolled that differs per machine. Item prices and difficulty do differ, so check before leaving
   it empty.
3. `<QuestName>AlternativeSolutionAcceptStrategy.cs` (`IAlternativeSolutionAcceptStrategy`), only if
   the quest has an alternative solution (sending troops).
4. Only if outcome branches pay differently: a `byte` enum `<QuestName>Branch.cs`, one patch class
   `Patches/<QuestName>BranchPatches.cs` with a prefix per branch that calls
   `IOutcomeDispatcher.BeforeQuestBranch`, and `<QuestName>FinalizationProofStrategy.cs` that maps each value
   to the vanilla branch method.
5. `<QuestName>Descriptor.cs` (`IQuestTypeDescriptor`). It names the issue type and takes the strategies
   in its constructor. Pass `null` for ones the quest does not have.
6. Check the quest's own event listeners that end it because of something the server sees. Wars, kingdom
   changes and captures need nothing: `Framework/Dispatch/QuestWorldEventRelayPatches.cs` runs the quest's
   `OnWarDeclared`, `OnClanChangedKingdom` or `OnHeroPrisonerTaken` on the server as the owner. The listener
   has to have exactly that name and vanilla's parameters, otherwise it is skipped. A listener with another
   name, or one for another event, needs a new relay patch in that file. Otherwise the quest never ends.
7. Only if the quest's vanilla code moves items or gold on the owner's client that the server already moves:
   a prefix on that method in `Patches/` that returns `ModInformation.IsServer`. Otherwise the owner gets it
   twice. Two places do this: the success the owner replays after the server ran it
   (`LordNeedsHorsesGiveMountsPatch`), and `AlternativeSolutionStartConsequence`, which vanilla's dialog runs
   on the client before the server accepts (`LordNeedsHorsesStartConsequencePatch`). Gold given with
   `GiveGoldAction` is already blocked on clients.

There is nothing to register. `IssuesModule` picks up everything in `Services.Issues.Quests` that
implements those interfaces, the gate lets the behavior run once its descriptor exists, and the patches
are found like every other Harmony patch.

## Trying it in game

Use a server and a client. All three commands are `coop.debug.issues.*`.

- `list_types` (both sides) lists the registered quest types.
- `give <hero_id> <quest_type>` (server) gives a notable or a lord an issue. Player heroes are refused.
- `complete <hero_id> [success|fail|betrayal|cancel|timeout]` (server) ends it. With an accepted quest it
  runs the outcome as the owner. Without one only `timeout` and `cancel` work. If the quest refuses the
  ending, for example a `LordNeedsHorses` hand-in when the owner does not carry the mounts, the command
  fails and the reason is in the server log.

Example on Danustica (`town_ES1`): stand in the town on a client and run
`coop.debug.settlements.list_notables`. It prints each notable's id and what they offer. On the server run
`coop.debug.issues.give <id of the gang leader> GangLeaderNeedsToOffloadStolenGoodsIssue`, accept it
from the client, then end it with `coop.debug.issues.complete <same id> success`.

To check who sees what, use two clients: the second client should see no quest, no pop-up and no
"updated" marker for the first client's quest, including after it leaves and rejoins.

## Tests

The in-process environment in `source/E2E.Tests` runs a server and clients without the game. See
`E2E.Tests/Services/Issues/`: `QuestScenario` sets up two connected players with a bare issue,
`UnstartedQuest` and `StubIssueQuestGenerator` stand in for a real quest, and the
`AcceptVisibilityTests`, `QuestSolutionAcceptRaceTests`, `QuestRequestOwnershipTests` and
`IssueCreationSyncTests` classes show the patterns. `ArmyNeedsSuppliesWarDeclaredTests` and
`LordNeedsHorsesQuestEventTests` build a real issue and quest instead of the stand-ins and cover the relayed
war, kingdom change and capture. `QuestEndingReplayTests` covers a client keeping its quest until the
server's ending arrives, and `OutcomeDispatcherRunAsOwnersTests` tests the dispatcher without Harmony.

## Known limits

- Lord Solution quests (4 of 43) are not supported.
- Only wars, kingdom changes and captures are passed to quests on the server, and only to listeners with
  vanilla's usual names.
- Quests are not kept across leaving or restarting. Troops sent when the server restarts are lost.
- Quest rewards give no trait XP for now. Vanilla keeps one shared store that every player would write to.
- When the server ends a quest, the owner's journal shows it as ended without vanilla's reason line, for
  example "Army is disbanded and your mission was canceled".
- Most of this has only been tested in the in-process environment. Giving an issue, accepting it, sending
  troops and a player rejoining have been tried by hand in a real game. Check the other paths in a real game
  before relying on them.

## Where things are

```
Framework/        shared code, knows nothing about any one quest
  Interface/        every interface, one file each
  CreationCapture/  creating an issue and sending its values
  AcceptCoordination/  accepting a quest or sending troops
  Finalization/     ending a quest and telling everyone
  Dispatch/         patches on vanilla's start and end methods, and the world-event relay to quests
  Registries/       quest type registry, who owns what, the join snapshot
  Resolvers/        turn ids into the issue, owner and descriptor
  Gating/           keeps unregistered vanilla issues off, and players and their clans from giving issues
  Dismissal/        ends quests when their player is gone
  Visibility/       keeps other players' missions off the quest screen
  Rewards/          skips shared trait XP
  Commands/         the debug commands
Quests/<QuestName>/ everything specific to one quest
```

Related code outside this folder: `Services/UI/Notifications/Patches/ServerQuestNotificationPatches.cs`
and `ServerQuestViewDataPatches.cs` (the server shows no quest pop-ups or unread markers), and
`Coop.Core/Server/Services/Issues/Handlers/IssueOwnershipSnapshotHandler.cs` (sends the join snapshot).
