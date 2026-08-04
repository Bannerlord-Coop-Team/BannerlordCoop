# Rival Gang Moving In — Design v2 (implementation-ready)

Branch: `feature/village-needs-tools-sync`. Supersedes the prior design from workflow `w60etqnga`, which was
never committed to `doc/` (it only exists as workflow output + the project memory's summary of its NO-GO). This
is a standalone rewrite, not a diff, produced to close out two disproven findings plus three smaller flagged
items. Verified directly against:

- Decompiled vanilla source: `SandBox/SandBox.Issues/RivalGangMovingInIssueBehavior.cs` (full class, both nested
  `RivalGangMovingInIssue` and `RivalGangMovingInIssueQuest`), `QuestBase.cs`, `QuestManager.cs`,
  `PlayerEncounter.cs` (`RestartPlayerEncounter`, `StartBattle`/`StartBattleInternal`, `SetupFields`),
  `QuestHelper.cs` (`CheckWarDeclarationAndFailOrCancelTheQuest`).
- This repo's checked-out state on `feature/village-needs-tools-sync` (clean tree, `git status` verified),
  specifically `DisableAllIssueBehaviorsExceptAllowlist.cs`, `VillageNeedsToolsIssueOwnership.cs`,
  `BettingFraudInstanceResolutionPatch.cs`, `NearbyBanditBaseIssueCreationPatch.cs` (creation-capture +
  ownership-gate shapes), `SmugglersPartySpawnGatePatch.cs` + `SmugglersIssueInterface.cs` (client-authority
  party-spawn gate shape — the direct precedent for §5), `IssueManagerTickPatches.cs` (confirms dedicated
  headless-server topology, `Hero.MainHero`/`MobileParty.MainParty` both null there, is in scope for this
  project), `PlayerEncounterPatches.cs` + `ConversationRequestHandler.cs` + `MapEventCreationCoordinator.cs`
  (the full existing client-request/server-authoritative pipeline reused unmodified by §6), `IssueAcceptancePatches.cs`
  + `GenericAcceptMirrorIssueTypes.cs` (accept-time mirror-eligibility convention), `IssueManagerCreateNewIssuePatches.cs`
  (confirms every non-VillageNeedsTools type needs its own creation-capture postfix).
- Confirmed via `git show c5e5e85a1 --stat`: `DisableRivalGangMovingInIssueBehavior.cs` was already deleted by
  the 19-file orphaned-disable-patch sweep. No orphaned patch currently blocks this type (re-grepped the tree,
  zero hits for `DisableRivalGang`).

## 0. What the prior design got wrong (both NO-GO findings, restated precisely)

**Finding A (disproven).** The prior design's core mechanism was a bespoke `RivalGangMovingInIssueInstanceScopingPatch`
— a `PendingQuest` stash plus a reflective `HourlyTick` reimplementation — built on the premise that two
players' concurrently-accepted Rival Gang quests collide inside the vanilla `Instance` singleton getter under
ordinary play. That premise is false, for the same reason it was false for Escort Caravan and Family Feud:
`IssueBase.StartIssueWithQuest()` — what every non-accepter's mirror-replay bare-calls — never calls
`QuestBase.StartQuest()`, and `StartQuest()` (`QuestBase.cs:152-163`) is the *only* thing that calls
`RegisterEvents()` or adds the quest to `QuestManager.Quests` (`QuestManager.OnQuestStarted`, the sole `_quests.Add`
call site, `QuestManager.cs:85-88`). So under ordinary bare-replay, a non-owner's mirror
`RivalGangMovingInIssueQuest` is inert by construction — it is never `IsOngoing`, never in `Quests`, and
`Instance`'s "first found in `Quests`" fallback can never resolve to it. No bespoke instance-scoping machinery
is needed for that scenario.

**But the concern is not baseless — it is real under a narrower trigger.** `QuestManager._quests`
(`QuestManager.cs:33`) is `[SaveableField(0)]` — a straight save/load field, populated directly by the
serializer on load, completely bypassing `StartQuest()`/`OnQuestStarted()`. Then `QuestManager.OnGameLoaded()`
(`QuestManager.cs:129-177`) calls `questBase.InitializeQuestOnLoadWithQuestManager()` — which **does**
unconditionally call `RegisterEvents()` (`QuestBase.cs:264-269`) — for every non-finalized quest in the
deserialized `_quests` list whose corresponding `IssueBase` is found in `Campaign.Current.IssueManager.Issues`.
Given this mod's per-peer-replicated-issue architecture (every peer independently constructs a mirror
`IssueBase`/`Quest` object for every issue in play, confirmed by `VillageNeedsToolsIssueOwnership`'s own type
doc comment: "each peer holds a DIFFERENT object instance representing the same logical issue"), the matching
`IssueManager.Issues` entry a newly-loaded quest needs *will* generally be present on every peer. Concretely:
if a mid-session save snapshot is ever transferred to a newly-joining client (or a save/reload happens while
two different owners' Rival Gang Moving In quests are concurrently active), the loading peer's own `_quests`
can genuinely end up holding **more than one owner's quest object, each for real `IsOngoing` and each with
`RegisterEvents()` genuinely called** — at which point vanilla's single-slot `_cachedQuest`/"first found"
`Instance` getter has no principled way to pick the right one for whichever menu/dialogue context is live on
that peer. This is the real, narrower risk this design targets (§3).

**Finding B (disproven).** The prior design proposed replaying `rival_gang_start_fight_on_consequence()` /
`StartAlleyBattle()` "on the server" so the whole vanilla alley-fight chain would run authoritatively there.
This doesn't work: `Campaign.Current.CurrentMenuContext`, `Mission.Current`, and `PlayerEncounter.Current` are
all per-process singletons (confirmed: `PlayerEncounter.Current`/`._mapEvent` are instance state on a
process-local static `Current` — see `PlayerEncounter.cs:639-648`, `925-928`). Running `StartAlleyBattle()` "on
the server" changes the **server's own** screen/menu/mission state, not the accepting **client's** — it does
nothing visible to the actual quest owner if the owner is a remote client. §6 redesigns this using the same
client-request/server-authoritative-data/client-opens-its-own-mission split already proven for ordinary
`PlayerEncounter` (`PlayerEncounterPatches.StartBattleInternalPrefix`), plus one genuinely new wrinkle specific
to this quest's synchronous scripted-battle call shape (§6.3).

## 1. Step 0 — allowlist

`DisableRivalGangMovingInIssueBehavior.cs` (the orphaned pre-allowlist patch) is already gone — deleted by the
19-file sweep, commit `c5e5e85a1` (`git show c5e5e85a1 --stat` lists it explicitly). `RivalGangMovingInIssueBehavior`
is confirmed **not** currently in `DisableAllIssueBehaviorsExceptAllowlist.cs`'s `Allowlist` set. Step 0 is a
single addition, no deletion:

```csharp
// Rival Gang Moving In (see doc/RivalGangMovingIn_Design_v2.md) — SandBox.dll, 2 spawned hostile parties + 2
// throwaway henchman Heroes, needs its own creation/accept/spawn/alley-fight infrastructure (§2-§6 below).
typeof(RivalGangMovingInIssueBehavior),
```

`VerifyAllowlistIntegrity()` will pass immediately once this lands — confirmed nothing else currently patches
this type's `RegisterEvents()`.

## 2. Issue-creation determinism (new, not previously discussed)

`RivalGangMovingInIssue`'s constructor takes `(Hero issueOwner, Hero rivalGangLeader)` directly
(`RivalGangMovingInIssueBehavior.cs:218-223`), and `rivalGangLeader` is picked at **issue creation time**, inside
`OnStartIssue` → `GetRivalGangLeader(issueOwner)` (`:1650-1662`): the first `Hero` in
`issueOwner.CurrentSettlement.Notables` satisfying `IsGangLeader && CanHaveCampaignIssues()`. `IssueManagerCreateNewIssuePatches`
already gates `IssueManager.CreateNewIssue` to server-only for **every** issue type generically (its own doc
comment: "creation is server-authoritative"), but its own capture/broadcast postfix is hardcoded to
`VillageNeedsToolsIssue` only — every other type needs its own creation-capture patch, following
`NearbyBanditBaseIssueCreationPatch.cs`'s exact shape (own independent postfix on the same `CreateNewIssue`
method, gated `if (!__result || ModInformation.IsClient) return;`, capturing+broadcasting a
`RivalGangMovingInIssueCreated(issue)` message so every peer's mirror is constructed with the SAME
`RivalGangLeader` reference (resolved by object-manager id) instead of each independently walking
`Notables`/rolling its own pick.

`GetRivalGangLeader`'s own walk is a plain deterministic-order `foreach` (no `MBRandom`/`GetRandomElement` roll
anywhere in it), so a bare uncorrected replay would likely already land on the same `Hero` on every peer in
practice — but "likely, by iteration-order coincidence" is exactly the kind of unverified claim this project's
own standing methodology (README §"How to apply") requires checking against decompiled source before trusting,
not asserting. Given the pattern (`GenericAcceptMirrorIssueTypes`) already exists specifically for "verified
byte-identical without capture," this needs the same one-time verification pass every other type went through
before deciding whether it can skip the bespoke creation patch — **not assumed safe here.** Budget for the
`RivalGangMovingInIssueCreationPatch.cs` regardless; it is cheap, and matches the established
"verify per type, don't trust, capture if any doubt" convention.

**New interface**: `IRivalGangMovingInIssueInterface` (mirrors `ISmugglersIssueInterface`'s shape) —
`TryCaptureRivalGangLeader(issue, out Hero)`, `ConstructReplicated(Hero owner, Hero rivalGangLeader)` (calls the
real public ctor directly — no reflection needed, same as Smugglers), `RegisterReplicated(...)` (same
`PotentialIssueData`-with-a-closure-factory technique as `SmugglersIssueInterface.RegisterReplicated`).

## 3. `Instance` getter fix — Finding A's real, narrower redesign

Exact precedent to match: `BettingFraudInstanceResolutionPatch.cs`. New file
`RivalGangMovingInInstanceResolutionPatch.cs`:

```csharp
[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior), "Instance", MethodType.Getter)]
internal class RivalGangMovingInInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __result = null;
        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest candidate
                && candidate.IsOngoing
                && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(candidate.QuestGiver))
            {
                __result = candidate;
                break;
            }
        }
        return false;
    }
}
```

This entirely bypasses the vanilla `_cachedQuest` field (same as BettingFraud's fix), so it is correct in BOTH
scenarios: ordinary play (where at most one owned quest can ever be in `Quests` on this peer, so the filter is
a no-op refinement over "first found") **and** the save/reload scenario from §0 (where `Quests` genuinely holds
multiple owners' entries — the ownership filter picks out only the one this peer's own `ControllerId` owns,
exactly as it needs to). All 4 of vanilla's `Instance`-consuming call sites are covered automatically since they
all read the same static getter: `rival_gang_wait_duration_is_over_menu_on_init` (`:1624`),
`rival_gang_quest_wait_duration_is_over_yes_consequence` (`:1643`), `rival_gang_quest_before_fight_init`
(`:1712`), `rival_gang_quest_after_fight_init` (`:1720`).

**Prerequisite**: `VillageNeedsToolsIssueOwnership` must actually have an entry for this quest's `QuestGiver` —
supplied by the accept-time handler in §4. Without §4, this fix is a no-op that always returns `null`.

**Residual, out-of-scope risk (flagged, not solved, matching this codebase's own established practice for this
exact class of gap — see `IssueManagerTickPatches.cs`'s and `LordWantsRivalCaptured`'s own documented,
unsolved save/reload-`RegisterEvents()`-reentry limitations)**: the save/reload scenario that makes the
`Instance` fix necessary *also* re-registers this quest's dialogue flows
(`GetRivalGangLeaderDialogFlow`/`GetQuestGiverPreparationCompletedDialogFlow`, added inside
`InitializeQuestOnGameLoad()`, `:773-786`) on a peer that may not be the true owner. A full fix for "who is
allowed to see/drive a foreign quest's dialogue after its object leaks into my own `_quests` via a save
transfer" is a bigger, cross-cutting architectural question (the same one flagged unresolved for `OnHeroKilled`
on Lord Wants Rival Captured) and is out of scope for this pass. §7 below adds cheap, narrowly-targeted
ownership gates on the two REWARD-bearing consequence paths this leak could reach, as defense-in-depth — not a
full solution to the underlying leak.

## 4. Accept-time ownership handler (new, not previously discussed — required for §3 to do anything)

New `RivalGangMovingInIssueHandler.cs`, following `BettingFraudIssueHandler.cs`'s exact 4-message shape
(`*QuestAcceptTriggered` → server: `SetOwner`+broadcast or client: `RequestAcceptQuest` →
`Network*QuestAccepted` → `Network*AcceptRejected`), keyed off `IssueQuestAcceptancePatch`'s existing
`VillageIssueQuestAcceptTriggered` publish (`IssueAcceptancePatches.cs:38-57`) once `RivalGangMovingInIssue` is
added to `GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible` (verify per §2's "no unverified claims"
rule: `GenerateIssueQuest(questId)` forwards `IssueOwner`/`RivalGangLeader`/a fixed `8`-day duration/`RewardGold`/
`IssueDifficultyMultiplier` — all frozen by creation time per §2, so this type likely qualifies for the generic
mirror, but confirm before relying on it instead of assuming). The handler's own job, independent of that:
`VillageNeedsToolsIssueOwnership.SetOwner(owner, controllerId)` at the moment of genuine accept — this is the
ONE piece every already-shipped type in this family needs bespoke (no generic accept handler populates the
ownership registry on its own).

Rival Gang Moving In has no alternative solution accept-time bespoke need beyond the existing generic
`IssueWithAlternativeSolutionAcceptancePatch` (its alternative-solution fields — `AlternativeSolutionHero`,
`AlternativeSolutionSentTroops` — are genuinely per-accepter-local like every other type's, not this quest's
concern; `AlternativeSolutionScaleFlags => (AlternativeSolutionScaleFlag)12` needs decoding against the enum to
confirm which of Duration/FailureRisk/Casualties apply before adding it to `AlternativeSolutionMirrorEligible`,
same "verify, don't assume" note as §2).

## 5. Party-spawn client-authority gate (Finding-adjacent, addresses the headless-`MainParty` critique item)

### 5.1 The gap, precisely

`StartAlleyBattle()` calls `CreateRivalGangLeaderParty()` and `CreateAllyGangLeaderParty()`
(`:1044-1102`), each of which calls `CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)`. Per this
project's own established, independently-verified finding for Smugglers (`SmugglersPartySpawnGatePatch.cs`'s
doc comment), that factory's inner `new CustomPartyComponent(...)` is hard-blocked on a client
(`CustomPartyComponentLifetimePatches.Prefix` returns false there), while the resulting bare `MobileParty` is
NOT blocked but IS silently orphaned (never registered/synced —
`GameInterface.Registry.Auto.LifetimePatches<MobileParty>.CreatePrefix` only logs on a client, doesn't stop the
constructor). Net effect on a remote-client owner, unmitigated: **two broken, split-brain ghost parties (rival
gang leader's and ally gang leader's) only that one client can ever see**, and the quest permanently stuck for
everyone else — the exact same bug shape Smugglers had, now confirmed present here too by the same code-reading
method (both methods call the identical factory).

### 5.2 The headless-server `MobileParty.MainParty` dependency — critique item, now closed with a real fix

Both methods also call `SettlementHelper.FindNearestHideoutToMobileParty(MobileParty.MainParty, (NavigationType)3, ...)`
(`:1055`, `:1085`) purely to pick which `Clan.BanditFactions` culture flavors the new party's `CustomPartyComponent`
under — `MobileParty.MainParty` is null on a dedicated headless server (confirmed in scope: `IssueManagerTickPatches.cs`'s
own doc comment states this explicitly and already works around it elsewhere in this exact issue family).
**Crucially, unlike Smugglers' `desiredMenCount`/`customPartyBaseSpeed` (genuinely MainParty-relative
percentages), the troop counts here are fixed constants** — `NumberOfRegularEnemyTroops = 15` /
`NumberOfRegularAllyTroops = 20` (`:512, 518`) — so nothing about these two methods is actually per-accepter-
divergent. The fix does **not** need to capture anything from the client's own `MainParty` and forward it (as
Smugglers' fix does); it can instead sidestep `MainParty` entirely, exactly the way
`SmugglersIssueInterface.CreateReplicatedSmugglerParty` already does for its own analogous hideout/culture pick:
substitute `SettlementHelper.FindNearestHideoutToSettlement(_questSettlement, MobileParty.NavigationType.Default)`
(a settlement-based overload, confirmed to exist and already used for exactly this purpose in
`SmugglersIssueInterface.cs:205`) in place of the `MobileParty`-based overload, using the quest's own
`_questSettlement` field (already available server-side, no player required). This closes the headless-server
gap completely and with strictly less machinery than Smugglers needed — no captured client-side value at all.

### 5.3 Design

New `IRivalGangMovingInIssueInterface` additions (extending §2's interface) — mirroring
`ISmugglersIssueInterface`'s shape exactly:

- `CreateReplicatedRivalGangLeaderParty(Hero owner)` / `CreateReplicatedAllyGangLeaderParty(Hero owner)`: faithful,
  parameterless (no per-accepter capture needed, per §5.2) reimplementations of `CreateRivalGangLeaderParty()`/
  `CreateAllyGangLeaderParty()`'s bodies, run ONLY on the server, using `FindNearestHideoutToSettlement` instead
  of `FindNearestHideoutToMobileParty`. Each also mints its henchman `Hero` via `HeroCreator.CreateSpecialHero`
  exactly as vanilla does — minted **only** server-side (same "never let a client independently mint a Hero"
  principle already required for Family Feud/Landowner's Daughter's Hero-minting gap), avoiding that family's
  known host-vs-client-accepter asymmetry bug entirely, since neither party nor Hero is ever locally constructed
  on a client under this design.
- Force-write helpers (reflection, `_rivalGangLeaderParty`/`_rivalGangLeaderHenchmanHero`/`_allyGangLeaderParty`/
  `_allyGangLeaderHenchmanHero` are all private) analogous to `ForceSmugglerParty`.

New `RivalGangMovingInPartySpawnGatePatch.cs` (mirrors `SmugglersPartySpawnGatePatch.cs`'s shape, applied to
BOTH methods):

```csharp
[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest))]
internal class RivalGangMovingInPartySpawnGatePatch
{
    [HarmonyPatch("CreateRivalGangLeaderParty")]
    [HarmonyPrefix]
    private static bool RivalGangLeaderPrefix(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true;   // host: vanilla unmodified, its own construction is unblocked
        // Already resolved by an earlier call in this same request (see §5.4) — no-op.
        if (/* _rivalGangLeaderParty already set */) return false;

        // Single combined blocking request creates BOTH parties + both henchman Heroes in one round trip —
        // see §5.4. No client-side capture needed (§5.2): nothing forwarded but the quest's own identity.
        RivalGangMovingInPartyCreationCoordinator.Instance.RequestBlocking(__instance);
        return false;
    }

    [HarmonyPatch("CreateAllyGangLeaderParty")]
    [HarmonyPrefix]
    private static bool AllyGangLeaderPrefix(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true;
        // The combined request above already populated this field too; nothing left to do.
        return false;
    }
}
```

### 5.4 `RivalGangMovingInPartyCreationCoordinator` — new, blocking, modeled directly on `MapEventCreationCoordinator`

Both scripted parties (and their henchman Heroes) must exist **before** `StartAlleyBattle()`'s very next line,
`PreparePlayerParty()`, runs — a fire-and-forget request (Smugglers' shape, which tolerates the party arriving
later via broadcast) does not work here, since the caller immediately dereferences `_rivalGangLeaderParty.Party`
a few lines later. This needs a **blocking** request, the same shape `MapEventCreationCoordinator.RequestBlocking`
already uses successfully for `StartBattleInternalPrefix` (`GameThread.WaitWhilePumping(() => pending.Completed.IsSet, deadline)`,
same `INetworkConfig.ObjectCreationTimeout`, same `PendingRequest`/`ManualResetEventSlim` shape). One request
(`NetworkRequestCreateRivalGangParties(ownerId)`) creates both parties + both henchman Heroes server-side in a
single game-thread-blocking call, then replies with all 4 object-manager ids once the AutoRegistry sync for the
2 `MobileParty`s + 2 `Hero`s has landed; the client force-writes all 4 quest fields before returning control.
Rejection/timeout: log and leave the 4 fields null — `StartAlleyBattle()` continuing past this point with null
parties would NRE identically to today's un-mitigated bug, so this is a graceful-abort case, not a new failure
mode, matching `StartBattleInternalPrefix`'s own `Unresolved`/`Rejected` handling.

## 6. Alley-fight trigger sync — Finding B's real redesign

### 6.1 Why `StartAlleyBattle()` structurally only ever runs on the genuine owner's own process

`StartAlleyBattle()`'s only call site is `rival_gang_quest_before_fight_init` (`[GameMenuInitializationHandler]`),
itself only reached when THIS peer's own `Campaign.Current.GameMenuManager` activates menu
`"rival_gang_quest_before_fight"` — which only happens as a direct, synchronous consequence of THIS peer's own
`rival_gang_start_fight_on_consequence()` firing, itself only reachable through a live one-to-one conversation
gated by `Hero.OneToOneConversationHero == QuestGiver` (`:906, 926`) inside a `DialogFlow` that is only ever
registered on the genuine owner's `ConversationManager` (via `OnQuestAccepted()` or the save/reload path flagged
in §3's residual-risk note). This is Category B in this project's own established terminology
(`SmugglersPartySpawnGatePatch.cs`'s doc comment: "a live `OfferDialogFlow.Consequence`, only ever reached on
the genuine accepter's own machine") — meaning `StartAlleyBattle()` never needs to be "sent to" or "replayed on"
any particular peer; it already only runs on the owner's own screen, by construction. The prior design's
"replay on the server" mechanism was solving a routing problem that does not exist — the real problem (below)
is entirely about what happens **inside** that one, already-correctly-scoped call.

### 6.2 The MapEvent-creation half is already correctly handled by existing, unmodified infrastructure

`StartAlleyBattle()` calls `PlayerEncounter.RestartPlayerEncounter(_rivalGangLeaderParty.Party, PartyBase.MainParty, false, false)`
(`:1036`) — confirmed via decompiled `PlayerEncounter.cs:639` to be the same single method
`RestartPlayerEncounter(PartyBase defenderParty, PartyBase attackerParty, bool forcePlayerOutFromSettlement = true, bool isPlayerEncounterRestartedForRaid = false)`
that `PlayerEncounterPatches.RestartPlayerEncounterPrefix` already patches — the 4th argument is merely an
optional parameter on the one overload, not a distinct overload the existing 3-parameter prefix would miss.
So on a remote-client owner this call is already correctly intercepted and routed through the existing pipeline
(`ConversationRequested` → `ConversationRequestHandler` → server approval → `NetworkAllowConversation` →
`AllowedThread`-wrapped real re-run) with **zero new code**. Likewise `PlayerEncounter.StartBattle()`
(`:1037`) already routes through the existing `StartBattleInternalPrefix`/`MapEventCreationCoordinator.RequestBlocking`
pipeline. No new patch is needed for either call in isolation.

### 6.3 The genuinely new problem: synchronous ordering, not routing

This is the concrete engineering gap the critique's "apply the same split... specifically" instruction points
at. `RestartPlayerEncounterPrefix`'s client branch is fire-and-forget (`return false` immediately after
publishing `ConversationRequested` — no blocking wait). Vanilla's real `RestartPlayerEncounter` body
(`PlayerEncounter.cs:639-648`) is what actually sets `PlayerEncounter.Current`; on a client that real body never
runs synchronously — only later, asynchronously, once server approval arrives and `Handle_NetworkAllowConversation`
re-runs it under `AllowedThread`. But `StartAlleyBattle()` calls `PlayerEncounter.StartBattle()` on the very
next line, and `StartBattle()`'s own body is `return Current.StartBattleInternal();` (`PlayerEncounter.cs:925-928`)
— a **direct, unguarded dereference of `Current`**. On a remote-client owner, `Current` is still null at that
point (the approval round-trip hasn't landed yet), so this NREs immediately — a real, newly-identified,
blocking correctness gap specific to this quest's synchronous scripted-battle-trigger shape (every other caller
of `RestartPlayerEncounter` in this codebase is tick/event-driven, not chained synchronously into an immediate
`StartBattle()` call the way this quest does).

**Fix**: a new Harmony Prefix on `StartAlleyBattle()` itself (`RivalGangMovingInAlleyBattlePatch.cs`) that, on a
client only, reimplements the method's existing sequence unchanged except for one insertion — a blocking wait,
using the exact same `GameThread.WaitWhilePumping`/`INetworkConfig.ObjectCreationTimeout` primitive
`MapEventCreationCoordinator.RequestBlocking` already uses — placed immediately after the
`RestartPlayerEncounter(...)` call and before `StartBattle()`:

```csharp
PlayerEncounter.RestartPlayerEncounter(_rivalGangLeaderParty.Party, PartyBase.MainParty, false, false);

if (ModInformation.IsClient)
{
    var deadline = DateTime.UtcNow + configuration.ObjectCreationTimeout;
    if (!GameThread.WaitWhilePumping(
            () => PlayerEncounter.Current != null
                && ReferenceEquals(PlayerEncounter.EncounteredParty, _rivalGangLeaderParty.Party),
            deadline))
    {
        Logger.Error("Timed out waiting for server-approved PlayerEncounter restart before the alley battle");
        return; // abort gracefully — proceeding into StartBattle() here would NRE against a still-null Current
    }
}

PlayerEncounter.StartBattle();
// ... rest of StartAlleyBattle()'s body, unchanged
```

The wait condition (`PlayerEncounter.Current != null && EncounteredParty == _rivalGangLeaderParty.Party`) mirrors
the exact duplicate-approval check `ConversationRequestHandler.Handle_NetworkAllowConversation` already uses
(`PlayerEncounter.EncounteredParty == defender || ... == attacker`), so it is not a novel comparison — confirmed
correct against `SetupFields` (`PlayerEncounter.cs:746-777`): with `attackerParty = PartyBase.MainParty` and
`defenderParty = _rivalGangLeaderParty.Party`, `_encounteredParty` resolves to `_rivalGangLeaderParty.Party`
exactly (the non-MainParty side). On the host-owns-the-quest path (`!ModInformation.IsClient`), this whole
branch is skipped and vanilla's real body runs synchronously and unmodified — `PlayerEncounter.Current` is set
immediately by the real `RestartPlayerEncounter`, so no wait is needed there, matching every other existing
patch in this family's `ModInformation.IsServer`/host-is-owner short-circuit.

No new network message is required for this wait — it rides the `ConversationRequested`/`NetworkRequestConversation`/
`NetworkAllowConversation` round-trip that `RestartPlayerEncounterPrefix` already triggers unmodified; this patch
only adds a local blocking wait for that existing flow's outcome to land.

The remainder of `StartAlleyBattle()`'s body (`_allyGangLeaderParty.MapEventSide = PlayerEncounter.Battle.GetMapEventSide(...)`,
`GameMenu.ActivateGameMenu(...)`, `_isReadyToBeFinalized = true`, `PlayerEncounter.StartCombatMissionWithDialogueInTownCenter(...)`)
needs no further changes: by the time `StartBattle()` returns, `MapEventCreationCoordinator.RequestBlocking`'s own
existing blocking wait already guarantees the MapEvent is fully resolved and committed on this client, and the
mission-with-dialogue opened at the end is inherently local to the owner's own screen (same as any other
in-mission conversation), needing no sync of its own.

## 7. Defense-in-depth ownership gates on save/reload-reachable reward paths (new, cheap, addresses §3's residual note)

Following the exact established precedent (`NearbyBanditBaseOwnershipGatePatches.cs`: gate only the
REWARD-bearing consequence methods reached by campaign-wide symmetric-event listeners, leave pure-cancel paths
alone since a generic cancel is already idempotent/safe via the existing `IssueFinalizedPatches` choke point),
of `RivalGangMovingInIssueQuest`'s 6 `RegisterEvents()` listeners (`:1216-1225`), 2 reach real
reward/penalty-applying consequence methods and are worth gating as cheap insurance against the §3 save/reload
leak; the other 4 only ever reach a bare `CompleteQuestWithCancel()` and are left alone, matching precedent:

- **`OnPlayerAttackedQuestGiverAlley`** (reached via `OnAlleyClearedByPlayer`/`OnAlleyOccupiedByPlayer` →
  `OnPlayerAlleyFightEnd`, `:1322-1365`): applies a `-150` Honor trait change, `-10` power, `-8` relation, `-10`
  town security, then `CompleteQuestWithFail`. Needs `VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver)`.
- **`OnSettlementOwnerChanged`** (`:1245-1254`): applies `-10` power + `-5` relation before cancel. Same gate.
- **`OnHeroKilled`**, **`OnSiegeEventStarted`**, **`OnClanChangedKingdom`**: all reach only
  `CompleteQuestWithCancel` with no reward/penalty — left ungated, matching precedent.
- **`OnWarDeclared`** → shared `QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest` (also dereferences
  `Hero.MainHero.MapFaction` directly, `QuestHelper.cs:140` — the same headless-server dependency already
  flagged for `IssueManager.DailyTick` elsewhere; not new to this quest, shared by every type using this
  helper). Verify at implementation time whether existing shipped types using this same helper already have
  their own gate on it (if a shared `QuestHelper`-level patch exists, reuse it; if each type gates it
  individually, add the same one-line prefix here) — not asserted here as a confirmed fact.

## 8. Smaller findings — resolved

### 8.1 `RestartPlayerEncounterPrefix` / `ConversationRequested` object-manager-id claim — verified, corrected

`PlayerEncounterPatches.RestartPlayerEncounterPrefix` (`PlayerEncounterPatches.cs:38-58`) does **not** resolve
any object-manager id itself — on a client it only constructs and publishes
`new ConversationRequested(defenderParty, attackerParty, forcePlayerOutFromSettlement, ConversationRestartSource.PlayerEncounter, false)`
with raw `PartyBase` references, then returns `false`. The actual object-manager-id resolution, and the entire
network round trip, live in `ConversationRequestHandler.Handle_ConversationRequested`
(`ConversationRequestHandler.cs:95-121`: `objectManager.TryGetIdWithLogging(request.DefenderParty, out var defenderId)` /
`...AttackerParty...`), which sends `NetworkRequestConversation` to the server; the server's
`TryAcceptConversationRequest`/`HoldAndApprove` decide approval; `Handle_NetworkAllowConversation`
(client-side) resolves the ids back to `PartyBase` objects and re-runs `RestartPlayerEncounter` under
`AllowedThread`. **Whichever prior write-up attributed object-manager-id handling to `RestartPlayerEncounterPrefix`
itself was incorrect** — that prefix is a thin, id-free trigger; `ConversationRequestHandler` is the actual
handler. This correction is load-bearing for §6.2/§6.3 above: it confirms our alley-fight fix needs no new
id-resolution code of its own, since `StartAlleyBattle()`'s calls ride this exact existing handler chain
unmodified.

### 8.2 `MobileParty.MainParty` on a dedicated headless server — real fix, not a "low risk" dismissal

Confirmed in scope: `IssueManagerTickPatches.cs`'s own doc comment states plainly that "the dedicated host has
no `MobileParty.MainParty`" and already works around exactly this for `IssueManager.HourlyTick`. §5.2 above
gives `CreateRivalGangLeaderParty`/`CreateAllyGangLeaderParty` a concrete fix: their only `MainParty` dependency
is a cosmetic hideout/culture pick, fully replaceable by the settlement-based
`SettlementHelper.FindNearestHideoutToSettlement(_questSettlement, ...)` overload — already precedented in this
exact codebase (`SmugglersIssueInterface.cs:205`) for the analogous Smugglers case — so the server-side
replicated creation methods never dereference `MobileParty.MainParty` at all. This is a complete fix for these
two methods, not a residual-risk note. (`PreparePlayerParty()`/`HandlePlayerEncounterResult()` also read
`MobileParty.MainParty`/`PartyBase.MainParty`, but per §6.1 these only ever run on the genuine owner's own
process — which, by construction, is never the dedicated server itself, since accepting/driving this quest
requires a live `Hero.MainHero`-backed conversation the headless server process can never have. No fix needed
there.)

## 9. Test plan

Following this project's own standing methodology (design → adversarial critique → implement with real E2E
tests, not a follow-up → independent review that reverts each claimed fix and confirms the exact test fails,
then restores):

1. `Instance` getter resolves the correct quest when two owners' `RivalGangMovingInIssueQuest` objects are both
   `IsOngoing` in one peer's `Quests` (construct this directly via test-only save/reload simulation rather than
   waiting on real save-transfer plumbing, matching how other ownership-gate tests in this suite are built).
2. Accept-time `SetOwner` populates the registry on all 3 topologies (host-is-owner, client-is-owner via
   request/broadcast, third-peer mirror never sets an entry for a quest it doesn't own).
3. Party-spawn gate: client-owner accept produces a single, correctly-AutoRegistry-synced pair of `MobileParty`s
   + henchman `Hero`s visible identically on host and every client (no ghost/orphaned party) — this is the
   direct regression test for the Smugglers-shaped bug this quest independently reproduces.
4. Headless-server creation: run the party-creation coordinator with `MobileParty.MainParty == null` (server
   process with no local player) and confirm no NRE and correct culture/hideout selection via `_questSettlement`.
5. Alley-fight synchronous ordering: client-owner path exercises the new blocking wait between
   `RestartPlayerEncounter` and `StartBattle()` under simulated network latency; confirm no NRE and that
   `PlayerEncounter.Battle`/`MapEventSide` are correctly populated before `GameMenu.ActivateGameMenu` runs.
   Include a timeout/rejection case (server denies or the wait deadline expires) and confirm graceful abort,
   not a crash.
6. Ownership gates (§7): confirm a non-owner mirror that (via simulated save/reload) has `RegisterEvents()`
   genuinely re-run cannot collect `OnPlayerAttackedQuestGiverAlley`'s/`OnSettlementOwnerChanged`'s reward
   penalties a second time.
7. Full regression: Issues E2E suite (currently 73/73 per project memory) + full unit suite, zero regressions,
   plus `VerifyAllowlistIntegrity()` passing cleanly for the new allowlist entry.

## 10. Verdict: **GO**

Both NO-GO findings are closed with concrete, minimal, precedented mechanisms (§3's ownership-filtered
`Instance` getter matching `BettingFraudInstanceResolutionPatch.cs` exactly; §6's client-request/
server-authoritative split matching `PlayerEncounterPatches`/`MapEventCreationCoordinator`, plus the one
genuinely new blocking-wait insertion §6.3 identifies and justifies precisely). All three smaller findings are
resolved: the `RestartPlayerEncounterPrefix`/`ConversationRequested` handler chain is now correctly attributed
(§8.1); the headless-server `MobileParty.MainParty` dependency in party creation has a real, complete fix, not a
dismissal (§8.2, §5.2); and the save/reload residual risk left open by §3 is closed cheaply, in the same
already-established shape, by §7. No new architecture is introduced beyond what two already-shipped precedents
(`BettingFraudInstanceResolutionPatch`, `SmugglersPartySpawnGatePatch`/`MapEventCreationCoordinator`) already
prove works in this codebase, plus one small, well-justified extension (§6.3's blocking wait) for this quest's
uniquely synchronous scripted-battle call shape. Build order: §1 (allowlist) → §2 (creation) → §4 (accept/
ownership) → §3 (`Instance` fix, now meaningful) → §5 (party-spawn gate + henchman Heroes) → §6 (alley-fight
sync) → §7 (defense-in-depth gates), each independently testable per §9 before moving to the next.
