# Quest learnings

Shared notes for anyone working on quests. Add things here that apply to more than one quest. Keep each entry
short: what goes wrong and what to do instead.

The rules in `AGENTS.md` come first. The PR numbers point at the merged PR where each problem was fixed, so the fix
can be copied.

## Updating this doc

Before a quest PR leaves draft, offer to update this doc (see GitHub Etiquette in the root `AGENTS.md`). Only add to
it once the user agrees:

1. Read the PR's review comments and see if anything learned from the suggested changes would help new quests.
2. Read the fix commits pushed after review or a live test. A bug found that late is often one other quests have.
3. Skip anything that only applies to that one quest, that isn't quest specific (that goes in `doc/CodeStyle.md`
   or the root Testing section), or that `AGENTS.md` or this doc already says.
4. Add each new point to the matching section as one short entry, what goes wrong and what to do instead, with
   the PR number. Only cite merged PRs or the PR being updated, so the fix is on development when the entry is.
5. Point at rules by their symbol or file, not their number, so a reorder of `AGENTS.md` doesn't break the entry.
6. Remove or update entries the PR made untrue, like a framework gap it closed.

## Common pitfalls

### Who runs what

- **Check how a world event reaches the quest.** The server's mirror quest is never started, so its own vanilla
  listeners never fire there. Check `QuestWorldEventRelayPatches` for the listener first. If it's missing, ask
  before adding a framework relay. `LordWantsRivalCapturedWorldEventPatches` is the per-quest fallback. (#3810)
- **A patched caller can skip AutoSync.** A method Harmony compiles before the AutoSync writer is installed inlines
  the unpatched setter (`Hearth`, `AddPower`), so the write never syncs. Queue the caller's patch through
  `IAutoSyncPatchCollector` like `RaidEventComponentRegistry`, or put the writer in `HARMONY_STATIC_FIXES_CATEGORY`
  like `HeroAddPowerPatch` in `HeroFieldPatches.cs`.

### Server authority

- **Ownership is not enough to accept an ending.** A request sent from a stale dialog or a double-click can arrive
  when the game state no longer allows it. Check the game state vanilla's condition checks on the server:
  `LordWantsRivalCapturedFinalizationProofStrategy` only accepts a capture it recorded as a `CaptureEntitlement`
  from the owner's battle, and `GangLeaderNeedsWeaponsDeliveryPatch` refuses a delivery away from the giver's
  settlement. (#3810, #3834)
- **Server checks must run as the owner.** `CanAccept`, fee and difficulty checks that run before
  `MainHeroSubstitutionScope` and `IssueDifficultyOverride` use the server's own `PlayerProgress`, which is per
  machine. Run them in the same scope as the charge. (#3834)
- **Check the owner can pay on the server.** `Hero.Gold` clamps at 0, so a fee charged without a server-side check
  lets the companion go out when the owner's gold dropped after the dialog. (#3834)
- **Don't be stricter than vanilla.** A server check vanilla doesn't have (a 120 skill companion when vanilla only
  checks the roster) rejects every client accept. Copy vanilla's condition exactly. (#3795)

### Persistence and rejoin

- **In-memory quest state must survive a load.** A service that fills its owner map only at accept is empty after
  a save and load or a rejoin, and a reset that runs after `LoadBehaviorData` wipes what was restored. Rebuild it
  from the save or the join snapshot, and check the `Campaign.OnInitialize` order. (#3821)

### Network

- **Send owner-only messages to the owner's peer.** `SendAll` for a journal or state message that only the owner
  reads sends it to everyone for every issue. Look the owner up in `IIssueOwnershipRegistry`. `issue.IssueOwner`
  is the NPC giver, not the player. (#3795)

## Review learnings

- **Decision callbacks reject on exception.** Wrap them like `AlternativeSolutionAcceptCoordinator.AcceptOnServer`
  does, or the client keeps waiting. (#3795)
- **Reading a capture fails cleanly on truncated data.** Check lengths before `BinaryReader.ReadString()`. (#3798)
- **Log skipped cases** in a finalization proof the way `ArmyNeedsSuppliesFinalizationProofStrategy` does. (#3795)
- **Keep `README.md` and `AGENTS.md` current** when a PR adds a registered quest or closes a known gap. (#3832)
