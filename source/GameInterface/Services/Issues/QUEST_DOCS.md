# Quest learnings

Shared notes for anyone working on quests. Add things here that apply to more than one quest. Keep each entry
short: what goes wrong and what to do instead.

The rules in `AGENTS.md` come first. The PR numbers point at where each problem was found, so the fix can be
copied.

## Updating this doc

When a quest PR is merged, strongly suggest updating this doc to the user. Only add to it once they agree:

1. Read the PR's review comments and see if anything learned from the suggested changes would help new quests.
2. Read the fix commits pushed after review or a live test. A bug found that late is often one other quests have.
3. Skip anything that only applies to that one quest, or that `AGENTS.md` or this doc already says.
4. Add each new point to the matching section as one short entry, what goes wrong and what to do instead, with
   the PR number.
5. Remove or update entries the PR made untrue, like a framework gap it closed.

## Common pitfalls

### Who runs what

- **The server's quest has no listeners.** The server's mirror quest is never started (rule 5), so a patch that
  waits for one of its vanilla listeners (`OnHeroPrisonerReleased`, `OnHeroPrisonerTaken`, `RaidCompleted`) never
  runs there, and the owner's client can't see the event. Check how the event reaches the quest. If it should end
  the quest, relay it from `QuestWorldEventRelayPatches` (rule 9) instead of a per-quest postfix pair. (#3810,
  #3832, #3796)
- **The owner's client times out the quest too.** The owner's client started the quest, so its `QuestManager`
  ticks run `CompleteQuestWithTimeOut` and send it as a request. A second timeout on the server makes one of them
  fail with `does not own`. Keep one path. (#3952)
- **Clients run the daily and hourly issue ticks.** `IssueManager` and `QuestManager` ticks run on every client.
  Only per-entity tickers are server only. A non-owner's issue with no journal entry throws in `DailyTick`
  (rule 4).
- **Patched vanilla methods skip AutoSync.** Harmony compiles a patched method before AutoSync rewrites field
  writes, so a field write inlined into a method you patch (`Hearth`, `AddPower`) stays on the server. A patch on
  a shared helper also changes every other caller. Patch the narrowest method and check that clients see the
  value. (#3766, #3781)
- **`MainParty` outside a substitution scope is the server's own party,** which the server removed. Vanilla code
  that reads `MainParty`, `Hero.MainHero` or `Campaign.Current.Player*` must run inside `MainHeroSubstitutionScope`
  (rule 6). Giver death during a companion mission lost the troops this way.
- **Never release a player prisoner inside `RunAsOwners`.** The release gives Roguery XP to the substituted hero
  and can capture the owner with their own party in a joined battle. Defer the release until the scope ends.
  (#3773, #3783)
- **Lost fights skip vanilla `OnMapEventEnded`.** A quest that reacts to its battle must handle the loss branch
  itself.

### Endings and finalize

- **Announce an ending once.** `CompleteQuestWithCancelDispatchPatch` already announces vanilla's
  `CompleteQuestWithCancel`. A quest patch that sends `QuestCancel` again, or a postfix after `IssueFinalized`
  (when `IssueOwner.Issue` is already cleared and the `OutcomeDispatcher.After` guard misses it), makes 2 or 3
  `NetworkIssueFinalized`. (#3796)
- **Giver death and executions end the issue without an owner scope.** `IssueManager.OnHeroKilled` and co-op
  executions finalize the server's mirror outside any owner scope, and cleanup that looks at `PlayerClan` finds
  nothing. If vanilla's cancel needs the owner's party and they have none at that moment, retry it later; don't
  drop it. (#3781, #3796)
- **A merge can undo a framework fix.** After merging development, re-read framework files your PR touched. A
  conflict resolution can quietly bring back the old version of a framework method.

### Server authority

- **Ownership is not enough to accept a request.** A request sent from a stale dialog or a double-click can arrive
  when the game state no longer allows it: `HearCounterOffer` before the target was beaten, `PersuasionSucceeded`
  after one failed try, a troop deposit right after sending the companion, a delivery away from the giver's
  settlement. A handler that checks only the owner and the generation applies it anyway. Check the game state that
  vanilla's dialog condition checks. (#3810, #3832, #3834)
- **Server checks must run as the owner.** `CanAccept`, fee and difficulty checks that run before
  `MainHeroSubstitutionScope` and `IssueDifficultyOverride` use the server's own `PlayerProgress`, which is per
  machine. Run them in the same scope as the charge. (#3834)
- **Check the owner can pay on the server.** `Hero.Gold` clamps at 0, so a fee charged without a server-side check
  lets the companion go out when the owner's gold dropped after the dialog. (#3834)
- **Don't be stricter than vanilla.** A server check vanilla doesn't have (a 120 skill companion when vanilla only
  checks the roster) rejects every client accept. Copy vanilla's condition exactly. (#3795)
- **Know where the money moves before patching.** Vanilla start consequences run on the client before the
  accept is intercepted, and #3832 took the gold locally that way. `GiveGoldAction.ApplyInternal` is blocked on
  clients now, so a consequence that only calls it needs no extra patch (#3834, #3800). Item moves still need one
  (Adding a quest type, step 6).
- **The owner's replay moves items a second time.** Replaying `CompleteQuestWithSuccess` on the owner ran
  `GiveMounts` again on top of the server's roster delta. Block item moves on the client (Adding a quest type,
  step 6). (#3832)

### Persistence and rejoin

- **Server-owned state set on the owner's client is lost.** `Hideout.IsSpotted` set only on the owner's client
  never reaches the save, so it's gone after a rejoin. Set server-owned state on the server. (#3800)
- **In-memory quest state must survive a load.** A service that fills its owner map only at accept is empty after
  a save and load or a rejoin, and a reset that runs after `LoadBehaviorData` wipes what was restored. Rebuild it
  from the save or the join snapshot, and check the `Campaign.OnInitialize` order. (#3821)
- **Static sets in patches outlive the session.** Clear them when the campaign ends, or keep them in a DI'd
  service. (#3796)
- **Subscriptions need an unsubscribe.** A strategy or handler that subscribes to a message implements
  `IDisposable` and unsubscribes. (#3810, #4000)
- **Sent troops must be cleared after they're returned.** A prefix that replaces vanilla's return helper has to
  clear `AlternativeSolutionSentTroops` too, or a retry returns them twice. (#3796)

### Network and cost

- **Send owner-only messages to the owner's peer.** `SendAll` for a journal or state message that only the owner
  reads sends it to everyone for every issue. Look the owner up in `IIssueOwnershipRegistry`. `issue.IssueOwner`
  is the NPC giver, not the player. (#3795, #3796, #3952)
- **Publish only on change.** State sent from `OnSettlementEntered` or `OnMapEventStarted` runs for every party on
  the map. Compare cheap fields (`JournalEntries.Count`) before building strings. (#3952)
- **Don't scan every issue every hour.** Keep the ids you care about and check only those. (#3796)
- **Debug capture must not build live objects.** `new Issue(...)` on the server with patches active registers a
  roster and sends it to every client, and it's never destroyed. Work the values out without constructing the
  issue. (#3834)

### Harmony

- **A class patched through `PatchAllUncategorized` needs a `[HarmonyPatch]` attribute,** even an empty one, or
  it's never applied. (#3821)
- **Helpers named `Prepare`, `Cleanup` or `TargetMethod(s)` run at patch time** with null arguments. A throw there
  stops all patching. (#3783)
- **One owner per patched method.** A `TargetMethods` that yields every method of a type also picks up a method
  another class patches, so the transpiler runs twice. Exclude it. (#3834)
- **Bind members with `nameof`.** The game assemblies are publicized, so use `nameof(Quest.OnWarDeclared)`, not a
  string lookup. `Framework/` is the exception because it can't name quest types (rule 9). (#3821)

### Vanilla facts that surprise

- Co-op quests have a blank `StringId` by design, so don't assert on it.
- Any finished raid, even one the militia repelled, costs the village's notables 5 power.
- `AdjustCaravansSpeed` never exits when the owner's party speed is under 1, for example while attached to an army.
  The server runs it as the owner, so guard it. (#3776)
- `IssueStayAliveConditions` can be stricter than the creation condition (a clan leader with a mount ratio under
  0.8 for horses). (#3832)

## Review learnings

- **Write the failing test first** for a reported bug, and say so in the PR. If it can't fail, reply with the
  evidence and add the test instead of changing code. The "handler runs twice" findings on #3806 and #3812
  assumed the server's mirror quest has listeners. It never does (rule 5), so both got a regression test and no
  code change.
- **Count events with `Assert.Single`, not `Assert.Contains`,** so a duplicate finalize or send fails the test.
  (#3796)
- **Use only `Framework/`.** A PR that brought back parts of the old `Generic` framework was blocked. Port to
  `Framework/` instead. (#3832)
- **Framework code doesn't know quests.** A shared handler like `MapEventResultsHandler` publishes a message that
  the quest's strategy subscribes to. When two quests need the same postfix, it goes in the framework. (#3810,
  #3832)
- **Decision callbacks reject on exception.** Wrap them like `AlternativeSolutionAcceptCoordinator.AcceptOnServer`
  does, or the client keeps waiting. (#3795)
- **Reading a capture fails cleanly on truncated data.** Check lengths before `BinaryReader.ReadString()`. (#3798)
- **Log skipped cases** in a finalization proof the way `ArmyNeedsSuppliesFinalizationProofStrategy` does. (#3795)
- **Layout:** one type per file. Branch patches go in `Patches/<QuestName>BranchPatches`, with the `<QuestName>Branch`
  enum in its own file. Reuse an existing helper or strategy instead of copying it (`Apply`, `SetState`, a
  projection). (#3795, #3798, #3800, #3952)
- **Remove patches that do nothing.** Reviewers ask why a patch exists when the path is already blocked. Check
  before adding one. (#3800, #3834)
- **Use `ContainerProvider.TryResolve(out var x)`** instead of ignoring the result and null-checking twice.
  (#3952)
- **Keep `README.md` and `AGENTS.md` current** when a PR adds a registered quest or closes a known gap. (#3832)
- **A flow the live-test tooling can't drive** means the tooling needs support, not a gap in the PR.
