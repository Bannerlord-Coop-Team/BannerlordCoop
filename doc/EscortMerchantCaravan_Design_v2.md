# Escort Merchant Caravan — Design v2 (implementation-ready)

Branch: `feature/village-needs-tools-sync`. Supersedes the original workflow-`w04dwzacg` design entirely (that
design was never committed to `doc/` — it only exists as prior workflow output + the project memory's summary
of it). This document is a standalone rewrite, not a diff. Verified directly against:
- Decompiled vanilla source: `EscortMerchantCaravanIssueBehavior.cs`, `QuestBase.cs`, `QuestManager.cs`,
  `MobileParty.cs` (confirmed `sealed`, no `operator ==`/`!=` override — reference-equality comparisons
  against a null field never throw).
- This repo's checked-out state on `feature/village-needs-tools-sync` (clean tree), specifically
  `DisableAllIssueBehaviorsExceptAllowlist.cs`, `VillageNeedsToolsIssueOwnership.cs`,
  `HeadmanNeedsToDeliverAHerdOwnershipGatePatches.cs`, `LandLordTheArtOfTheTradeOwnershipGatePatches.cs`,
  `HeadmanNeedsGrainOwnershipGatePatches.cs`, `LordNeedsGarrisonTroopsInstanceResolutionPatch.cs`,
  `BettingFraudInstanceResolutionPatch.cs`, `IssueManagerTickPatches.cs`, `MobilePartyRegistry.cs`,
  `TransferSaveState.cs`, `CampaignState.cs`, `GameSaveDataPacketHandler.cs`.

## 0. What the original design got wrong

The original design's core mechanism was a new gate blocking the quest's own tick/listener methods on
non-owner peers, on the theory that every peer's mirror independently runs the quest's own logic. That
premise is false and already disproven, twice over, elsewhere in this exact codebase
(`LordNeedsGarrisonTroopsInstanceResolutionPatch.cs`, `BettingFraudInstanceResolutionPatch.cs`):
`IssueBase.StartIssueWithQuest()` — what every non-accepter's mirror-replay bare-calls — never calls
`QuestBase.StartQuest()`, and `StartQuest()` is the only thing that calls `RegisterEvents()` or adds the quest
to `QuestManager.Quests`. So under ordinary play, a non-owner's mirror `EscortMerchantCaravanIssueQuest`
object is inert by construction: none of its 7 `RegisterEvents()` listeners or its `HourlyTick`/`DailyTick`
overrides ever run there. No gate was needed for that scenario, and building one was solving a problem that
doesn't occur.

That correct insight, however, is not the whole story for this quest type. Two things the original design
missed, addressed in full below:

1. **The quest's constructor runs on every peer, unconditionally, and calls `SetDialogs()` before ownership is
   even knowable.** `SetDialogs()` registers 5 dialogue flows globally; 3 of them share a condition delegate
   that dereferences `_questCaravanMobileParty` — which is only ever set by `SpawnCaravan()`, itself reachable
   only through the real accepter's own live `QuestAcceptedConsequences()` — with no null check. This is a
   real, guaranteed-reachable NRE, and it is **not** an ownership problem (nobody is "the owner" yet at
   construction time) — it needs its own, narrower fix (§2).
2. **`QuestManager.OnGameLoaded()` → `InitializeQuestOnLoadWithQuestManager()` is a second, genuinely-live
   code path** that *does* call `RegisterEvents()` (and re-calls `SetDialogs()`) — for real, on whichever
   peer(s) legitimately have this quest in their own `QuestManager.Quests` after any reload, including a
   client joining mid-quest. This is not the inert-mirror case the original design (correctly) ruled out; it
   needs a real fix (§3), and it is also the correct causal explanation for why the timeout path (§4) needs an
   ownership gate — not "independent convergence."

## 1. Step 0 — allowlist state (smaller than previously recorded)

`DisableEscortMerchantCaravanIssueBehavior.cs` (the orphaned pre-allowlist patch that unconditionally no-op'd
`RegisterEvents()`) **no longer exists** — it was one of the 19 files deleted by the orphaned-disable-patch
sweep, commit `c5e5e85a1`. Verified via `git log --all -- "*EscortMerchantCaravan*"`, which shows only that
deletion commit plus 3 older, unrelated commits (a Harmony-policy-gate pass and two merges) — no file has ever
re-added it. `EscortMerchantCaravanIssueBehavior` is confirmed **not** currently in
`DisableAllIssueBehaviorsExceptAllowlist.cs`'s `Allowlist` set either (grepped directly, zero hits).

Net effect: Step 0 for this type is reduced to a single addition, no deletion:

```csharp
// Escort Merchant Caravan (see doc/EscortMerchantCaravan_Design_v2.md)
typeof(EscortMerchantCaravanIssueBehavior),
```

added to `Allowlist` in `source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs`.
`VerifyAllowlistIntegrity()` will pass immediately once this lands (nothing else patches this type's
`RegisterEvents()`), unlike several other Tier-2 types where this step recurringly needed both a deletion and
an addition.

## 2. Bug A — `SetDialogs()` NRE on every peer at construction time

### 2.1 Exact mechanism

`EscortMerchantCaravanIssueQuest`'s constructor (runs via `GenerateIssueQuest()`, which every peer's own
`IssueManager.StartIssueQuest`/`IssueBase.StartIssueWithQuest` replay reaches unconditionally, real accepter
and mirrors alike):

```csharp
public EscortMerchantCaravanIssueQuest(...) : base(...)
{
    ...
    SetDialogs();
    InitializeQuestOnCreation();   // -> AddDialogs(): only OfferDialogFlow/DiscussDialogFlow/QuestCharacterDialogFlow
}

protected override void SetDialogs()
{
    OfferDialogFlow = ...Condition(() => Hero.OneToOneConversationHero == base.QuestGiver)...       // safe
    DiscussDialogFlow = ...Condition(() => Hero.OneToOneConversationHero == base.QuestGiver)...     // safe
    Campaign.Current.ConversationManager.AddDialogFlow(GetCaravanPartyDialogFlow(), this);          // UNSAFE
    Campaign.Current.ConversationManager.AddDialogFlow(GetCaravanGreetingDialogFlow(), this);       // UNSAFE
    Campaign.Current.ConversationManager.AddDialogFlow(GetCaravanTradeDialogFlow(), this);          // safe
    Campaign.Current.ConversationManager.AddDialogFlow(GetCaravanLootDialogFlow(), this);           // safe
    Campaign.Current.ConversationManager.AddDialogFlow(GetCaravanFarewellDialogFlow(), this);       // UNSAFE
}
```

The 5 flows registered here are global conversation content, keyed to shared dialogue-state tokens
(`"start"`, `"escort_caravan_talk"`) — the same states used by every other concurrently-active Escort Caravan
quest on the campaign, and (for `"start"`) potentially anyone's ordinary conversation. `ConversationManager`
evaluates every registered flow's `Condition()` tied to a reachable state when building that state's option
list, regardless of which specific quest instance the flow came from. So this isn't "only a problem for this
quest's own giver/caravan" — it's evaluated any time **any** peer's conversation reaches these states.

### 2.2 Which delegates actually dereference the null field (checked one by one)

Only **one** condition delegate is unsafe, and it backs 3 of the 5 flows (registered 3 separate times, same
delegate):

| Dialog flow | Condition delegate | Verdict |
|---|---|---|
| `GetCaravanPartyDialogFlow()` | `caravan_talk_on_condition` | **Unsafe** — `_questCaravanMobileParty.MemberRoster.Contains(...)` is the very first expression evaluated, no null check |
| `GetCaravanGreetingDialogFlow()` | `caravan_talk_on_condition` | **Unsafe** — same delegate |
| `GetCaravanFarewellDialogFlow()` | `caravan_talk_on_condition` | **Unsafe** — same delegate |
| `GetCaravanTradeDialogFlow()` | `caravan_buy_products_on_condition` / `conversation_caravan_player_trade_end_on_condition` | Safe — only ever does `MobileParty.ConversationParty == _questCaravanMobileParty` (reference comparison); confirmed `MobileParty` is `sealed` with no `operator ==`/`!=` override, so comparing against a null field never dereferences it |
| `GetCaravanLootDialogFlow()` | `caravan_loot_on_condition` | Safe — same shape, reference comparison only |

So the crash is entirely owned by **one method**: `caravan_talk_on_condition()` (private instance method on
`EscortMerchantCaravanIssueQuest`). It's the first thing evaluated whenever `ConversationManager` walks the
`"start"` or `"escort_caravan_talk"` dialogue states while *any* non-owner mirror of this quest type exists
anywhere in the campaign (created the moment the issue is offered to any hero — i.e. routinely, not rarely).

### 2.3 Two options, weighed

**Option A — null-guard `caravan_talk_on_condition()` only.**
A single Harmony prefix on the one unsafe method:

```csharp
[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest), "caravan_talk_on_condition")]
internal class EscortMerchantCaravanCaravanTalkConditionNullGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (GetQuestCaravanMobileParty(__instance) == null)
        {
            __result = false;   // no caravan yet on this mirror -> this dialogue option can never legitimately apply
            return false;       // skip original, avoid the NRE
        }
        return true;            // caravan exists (real owner, or a legitimately-resolved joiner) -> run vanilla logic
    }

    private static MobileParty GetQuestCaravanMobileParty(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest instance) =>
        (MobileParty)AccessTools.Field(instance.GetType(), "_questCaravanMobileParty").GetValue(instance);
}
```

- Pros: smallest possible diff; touches exactly the one method that is actually broken; leaves the other 4
  flows (including the load-bearing `OfferDialogFlow`/`DiscussDialogFlow` that the real accepter needs)
  completely untouched, so accept-ability is never at risk; matches this codebase's existing preference for
  narrow, named-method Harmony patches over broad suppression (see every `*OwnershipGatePatches.cs` file).
- Cons: relies on correctly identifying every unsafe delegate by hand; if a future vanilla patch adds a 6th
  flow with the same shape, it would need its own review to catch.

**Option B — suppress `SetDialogs()`'s registration of the 5 caravan-specific flows for non-owner mirrors.**
This was the framing implied by the task brief, but it does not actually work as stated: at construction
time, *no* peer is "the owner" yet — `VillageNeedsToolsIssueOwnership`-style ownership is only established
after a genuine accept is broadcast and confirmed, which happens strictly *after* `SetDialogs()` already ran
in the constructor on every peer including the eventual accepter. Suppressing the whole registration
"for non-owners" is therefore not well-defined at the point `SetDialogs()` runs — there is no owner to compare
against yet. A cruder version (suppress unconditionally, always, everywhere) would also kill
`OfferDialogFlow`/`DiscussDialogFlow`... except those aren't part of the 5 imperative
`AddDialogFlow(...)` calls this option would target — they're separate fields consumed later by `AddDialogs()`
— so a *correctly scoped* Option B (touching only the 5 imperative calls, not the two safe fields) is at least
causally coherent. But it still throws away the 2 already-safe flows (Trade, Loot) for every mirror, which is
unnecessary collateral, and it's a strictly larger, less precise change for no additional safety over Option A
(both flows are already immune to this specific NRE).

**Recommendation: Option A.** It fixes the exact, fully-identified defect with the smallest possible surface,
matches this repo's established narrow-patch convention, and doesn't touch the two flows that were never
broken.

### 2.4 The fix must also cover the second registration site

`InitializeQuestOnGameLoad()` (line ~1249 in the decompiled source) calls `SetDialogs()` again:

```csharp
protected override void InitializeQuestOnGameLoad()
{
    MobileParty questCaravanMobileParty = _questCaravanMobileParty;
    if (questCaravanMobileParty != null && questCaravanMobileParty.IsCaravan) CompleteQuestWithCancel();
    SetDialogs();
}
```

Because Option A patches the shared `caravan_talk_on_condition` method itself (not the registration call
site), this second registration is automatically covered — no separate patch needed. This is an additional,
concrete reason to prefer Option A over any variant of Option B that patches `SetDialogs()`'s call sites
directly, which would need to be applied twice.

## 3. Bug B — `InitializeQuestOnLoadWithQuestManager` is a real, live code path

### 3.1 Confirmed mechanism (traced end-to-end)

```
QuestManager.OnGameLoaded(...)                                  // QuestManager.cs:129
  for each quest in this peer's own (deserialized) Quests list
    if !quest.IsFinalized && issue.Value?.IssueQuest == questBase
      questBase.InitializeQuestOnLoadWithQuestManager()          // QuestBase.cs:264
        RegisterEvents()                                         // subscribes all 7 CampaignEvents listeners, for real
        InitializeQuestOnGameLoad()                               // re-calls SetDialogs()
        AddDialogs()
```

`QuestManager.Quests` is populated *only* by `OnQuestStarted` (`StartQuest()`'s own call), so under ordinary
play only the genuine owner's own process ever has this quest in that list, and `OnGameLoaded`'s loop only
ever touches it there — matching vanilla single-player behavior and requiring no new synchronization. This
path becomes materially different from the inert-mirror case in exactly two situations, both real and neither
rare:

- **The genuine owner's own reconnect/resync.** Ordinary, frequent (autosave reload, brief disconnect/rejoin,
  dedicated-server restart) — already anticipated in this codebase's own precedent
  (`HeadmanNeedsToDeliverAHerdOwnershipGatePatches.cs`'s doc comment makes the identical observation for a
  different quest type).
- **A client joining mid-quest.** `Coop.Core.Server.Connections.States.TransferSaveState` takes a genuine
  `saveInterface.SaveCurrentGame()` snapshot of the host's full campaign state and sends it to the joining
  client as a real, compressed save blob (`GameSaveDataPacket`/`GameSaveDataChunkPacket`,
  reassembled by `GameSaveDataPacketHandler` into `NetworkGameSaveDataReceived`). This is not the generic
  issue-mirror replication path (`IssueBase.StartIssueWithQuest`) the original design correctly ruled out — it
  is a load of the actual saved `QuestManager.Quests` list through the normal game-load pipeline, so
  `OnGameLoaded` fires for real on the joining client too, for any quest that was already genuinely started
  before the snapshot was taken.

### 3.2 Why this matters: it is not just a null-field risk, it's a live-listener risk

Because `RegisterEvents()` genuinely runs here (unlike the constructor-mirror case), **every** listener it
subscribes is live from that point on, for however many peers legitimately hold a copy. Reading each listener
against the decompiled source turns up unguarded `_questCaravanMobileParty` dereferences well beyond the
dialogue conditions in §2 — these are separate defects, not restatements of Bug A, and they matter only on
this path:

| Method | Trigger | Unguarded dereference |
|---|---|---|
| `OnWarDeclared` | `CampaignEvents.WarDeclared` — fires for **any** war declared anywhere in the campaign, not just ones involving this quest | `faction1 == _questCaravanMobileParty.MapFaction` at the top of the method, no null check (a second check further down *is* null-guarded, but that's not the one that crashes first) |
| `OnPartyHourlyTick` → `CheckPartyAndMakeItAttackTheCaravan` | `CampaignEvents.HourlyTickPartyEvent` — fires once per hour **per active MobileParty on the map**, i.e. potentially hundreds of times/hour | `mobileParty.Party.NumberOfHealthyMembers <= _questCaravanMobileParty.Party.NumberOfHealthyMembers`, no null check |
| `OnPartyHourlyTick` → `CheckEncounterForBanditParty` | same | `_questCaravanMobileParty.IsActive`/`.MapEvent`/`.CurrentSettlement`/`.Position`, no null check |
| Quest's own `HourlyTick()` override | Every in-game hour, for as long as this peer's copy is in `Quests` | `_questCaravanMobileParty.TargetSettlement` — only guarded by `base.IsOngoing`, not by a null check on the field |

`CheckOtherBanditPartyDistance` only ever does `== _questCaravanMobileParty` reference comparisons (safe, same
reasoning as §2.2). `OnSettlementEntered`/`OnSettlementLeft`/`OnMapEventEnded` all read the field but are
already reached only after an equality/null check in vanilla's own code — not an NRE risk, but see §3.3 for why
they still need gating.

**Net severity**: on this path, the single most dangerous entry point is `OnPartyHourlyTick` — it fires for
every party on the map, every hour, so a peer with a live-but-null `_questCaravanMobileParty` on this path
would crash almost immediately, not eventually.

### 3.3 The duplicate-application risk (separate from, and in addition to, the NRE risk)

Even where `_questCaravanMobileParty` *does* correctly resolve (the ordinary case — see §3.4), having more
than one peer's `QuestManager.Quests` legitimately contain this quest at once, each with live listeners, means
each peer's own `HourlyTick`/`OnSettlementEntered`/etc. independently perform the **same world-mutating
actions**: `ActivateBanditParty()` (which builds its bandit party's id as `"escort_caravan_quest_" +
base.StringId` — identical on every peer holding this same deserialized quest object, since `StringId` is part
of the shared save state — a guaranteed id collision against the AutoRegistry-tracked `MobileParty` namespace
if two peers both attempt it), `SuccessConsequences()`'s gold/relation/power/prosperity payout, and
`FailConsequences()`'s penalties. This is a correctness bug independent of whether any dereference actually
throws.

### 3.4 Recommended fix

Ownership-gate the full live-listener surface to the single recorded owner, reusing the existing
`VillageNeedsToolsIssueOwnership` registry exactly as already established for other quest types
(`HeadmanNeedsToDeliverAHerdOwnershipGatePatches.cs`, `LandLordTheArtOfTheTradeOwnershipGatePatches.cs`), just
applied to a larger method set here because — per this project's own standing note — Escort Caravan's entire
lifecycle (not just its turn-in) is ambient-tick/event-driven:

```csharp
[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest))]
internal class EscortMerchantCaravanOwnershipGatePatches
{
    [HarmonyPatch("OnSettlementEntered")]   [HarmonyPrefix] private static bool OnSettlementEnteredPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnSettlementLeft")]      [HarmonyPrefix] private static bool OnSettlementLeftPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnMapEventEnded")]       [HarmonyPrefix] private static bool OnMapEventEndedPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnWarDeclared")]         [HarmonyPrefix] private static bool OnWarDeclaredPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnClanChangedKingdom")]  [HarmonyPrefix] private static bool OnClanChangedKingdomPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnPartyHourlyTick")]     [HarmonyPrefix] private static bool OnPartyHourlyTickPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnSettlementOwnerChanged")] [HarmonyPrefix] private static bool OnSettlementOwnerChangedPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("HourlyTick")]            [HarmonyPrefix] private static bool HourlyTickPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("DailyTick")]             [HarmonyPrefix] private static bool DailyTickPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);
    [HarmonyPatch("OnTimedOut")]            [HarmonyPrefix] private static bool OnTimedOutPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    private static bool Gate(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest instance) =>
        CallOriginalPolicy.IsOriginalAllowed() || VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(instance.QuestGiver);
}
```

This single change closes both the §3.2 unguarded-NRE surface (nothing on the non-owner side ever runs far
enough to touch the null field) and the §3.3 duplicate-application risk (exactly one peer ever executes any of
this), and it is the same mechanism §4 needs for `OnTimedOut` — see below.

**Prerequisite this fix depends on, not yet designed here**: `VillageNeedsToolsIssueOwnership.SetOwner` needs
a real call site for Escort Caravan. Today it's only populated by Village Needs Tools/Crafting Materials'
own accept-broadcast handlers. This quest type needs its own analogous accept handler (broadcasting
"issue X accepted by controller Y" the same way `VillageNeedsToolsIssueHandler` does) — likely the natural
home for the creation-time `_companionRewardRandom` capture already flagged as sound in the prior design
(§2 there) and carried forward unchanged here (see §5). Also flagged, matching this registry's own documented
fragile-coupling note: persistence of the ownership dictionary across a reload currently piggybacks
specifically on `VillageNeedsToolsIssueBehavior.SyncData` — Escort Caravan's own accept handler needs either
to reuse that same hook or give the registry its own type-neutral persistence path; this document does not
resolve which, and implementation must pick one explicitly rather than silently assume it's already covered.

### 3.5 Defensive fallback: resolve `_questCaravanMobileParty` by component, don't just trust the deserialized field

Separately from the ownership gate above (which is needed regardless), add a narrow, fail-safe fallback inside
`InitializeQuestOnGameLoad()` for the one peer the gate identifies as the genuine owner:

```csharp
protected override void InitializeQuestOnGameLoad()
{
    // ... existing null/IsCaravan check ...
    if (_questCaravanMobileParty == null && base.IsOngoing)
    {
        _questCaravanMobileParty = MobileParty.All.FirstOrDefault(mp =>
            mp.PartyComponent is CustomPartyComponent cpc &&
            cpc.PartyOwner == base.QuestGiver &&
            mp.HomeSettlement == base.QuestGiver.CurrentSettlement);

        if (_questCaravanMobileParty == null)
        {
            CompleteQuestWithCancel();   // fail safe, same shape as the existing IsCaravan branch above
            return;
        }
    }
    SetDialogs();
}
```

Rationale: the caravan `MobileParty` itself is guaranteed to already be present and correctly identified on
every peer via the generic `MobilePartyRegistry`/`AutoRegistryBase<MobileParty>` mechanism — its creation
(via the patched `MobileParty` constructor `SpawnCaravan()` invokes) replicates network-wide independently of
whatever the Quest object's own `_questCaravanMobileParty` field happens to say. This makes an explicit,
component-based re-resolution a cheap, always-safe defense: if the deserialized field reference genuinely
"just works" (the expected case for a clean full-save transfer, since `_questCaravanMobileParty` is a real
`[SaveableField(4)]` and the snapshot is one internally-consistent object graph), this fallback never
triggers. If it doesn't — e.g. any divergence between vanilla's own save-time object-graph identity and this
mod's separate, parallel `IObjectManager`/AutoRegistry identity scheme that this design pass could not fully
trace without a live join test — the fallback closes the gap instead of crashing. **Flagged as an open item**
(§6): whether this fallback is ever actually exercised in practice needs a real join-mid-quest integration
test; this design commits to including it regardless, on the same "costs nothing, don't assume the risk is
unreachable" reasoning already established as precedent in this codebase
(`LordNeedsGarrisonTroopsInstanceResolutionPatch.cs`'s own doc comment).

## 4. §4 (was "§5") — timeout: corrected reasoning

### 4.1 What was wrong with "independent convergence"

The task brief's own framing to correct — and, it turns out, a phrasing that already exists verbatim in two
other files in this codebase (`LandLordTheArtOfTheTradeOwnershipGatePatches.cs`,
`HeadmanNeedsGrainOwnershipGatePatches.cs`, both: *"every peer's own mirrored quest independently reaches
[the timeout], and would otherwise apply the mutation once per connected peer instead of once, total"*) — is
imprecise for the same reason §0 already established: under ordinary play, a non-owner's mirror is never in
`QuestManager.Quests` at all, so `QuestManager.HourlyTick()`'s per-quest `QuestDueTime.IsPast` sweep
(`QuestManager.cs:189`) never even looks at it. There is no "every peer independently reaches the same
due-time and happens to converge" — most peers structurally can't reach it in the first place.

### 4.2 What actually happens

`OnTimedOut()` is reached via `CompleteQuestWithTimeOut()`, called from `QuestManager.HourlyTick()`'s own
per-quest sweep over `Quests` — not via any `CampaignEvents` listener `RegisterEvents()` subscribes, so it's a
separate trigger mechanism from every method in §3.4's table even though it needs the exact same fix.
Concretely:

- Under ordinary play (no reload/join since accept), only the genuine owner's own process has this quest in
  `Quests`, so `OnTimedOut` fires exactly once, naturally, with zero coordination needed — identical to
  vanilla single-player.
- The real multi-application risk is the **same** mechanism as §3: after a reload/reconnect (the owner's own)
  or a mid-quest join (a new client), `QuestManager.OnGameLoaded` can legitimately populate **more than one**
  peer's own local `Quests` with a copy of this quest, and each such peer's own `QuestManager.HourlyTick()`
  will, independently and correctly by its own local lights, notice `QuestDueTime.IsPast` and call
  `OnTimedOut()` for real — applying `AddPower(-5f)`/relation/`Town.Prosperity -= 20f` once per such peer
  instead of once, total.
- The fix is exactly the ownership gate already in §3.4's table (`OnTimedOut` is already listed there) — this
  section exists to correct *why* it's needed, not to add a new patch.

### 4.3 "Broadcast+bare-replay", not independent re-derivation

Once `OnTimedOut` is gated to the single recorded owner, that owner's own local vanilla code is what actually
mutates `Hero.AddPower`/relationship/`Town.Prosperity` — real, local field writes, not a bespoke Issues-level
network message. Those fields are ordinary AutoSync-tracked campaign state (the same generic Hero/Settlement
field-sync mechanism every other synced mutation in this codebase already rides on) — the owner's write
propagates to every other peer as a plain field-value broadcast, and each of them **applies the already-decided
value directly** rather than re-running `OnTimedOut`'s own logic locally. That's the "broadcast+bare-replay"
shape the task asked this section to reflect: one authoritative local execution, then propagation of its
*result*, not N peers each independently re-deriving the same outcome (which both the old "independent
convergence" phrasing implied and which, per §4.1, isn't even structurally possible for most peers to begin
with).

### 4.4 Side finding, not fixed here

`LandLordTheArtOfTheTradeOwnershipGatePatches.cs` and `HeadmanNeedsGrainOwnershipGatePatches.cs` carry the
same imprecise "independent convergence" framing in their own doc comments today. Their actual *patches* are
unaffected (the gate they apply is correct regardless of the reasoning written above it — gating to the
recorded owner is right either way), so this is a documentation-accuracy issue, not a functional bug, and
fixing it is out of scope for an Escort-Caravan-focused design. Worth a small follow-up comment correction in
both files at some point so the misconception doesn't get copy-pasted into a future type's design the way the
"independent mirror tick" premise itself got copy-pasted into this quest's original design.

## 5. What's carried forward unchanged from the original design

Per the project memory, two pieces of the original design were sound and are not revisited here:

- **§2 creation-time `_companionRewardRandom` capture** — this field is rolled once in the Issue's own
  constructor (`_companionRewardRandom = MBRandom.RandomInt(3, 10)`) and feeds `RewardGold`, which
  `GenerateIssueQuest` forwards into the Quest's own `rewardGold` constructor parameter. Same shape as every
  other type's creation-time capture already in `GenericAcceptMirrorIssueTypes`'s doc comments — needs the
  same treatment (force-write at accept time via the new accept handler this design's §3.4 prerequisite
  already calls for) so a bare replay on other peers reconstructs a byte-identical value instead of re-rolling.
- **§3b alternative-solution reuse** — `IsThereAlternativeSolution == true`,
  `AlternativeSolutionScaleFlags == Casualties | FailureRisk` (same shape as `CapturedByBountyHuntersIssue`,
  already proven safe to route through the existing generic `MirrorAlternativeAccepted`/
  `NewIssueTypesAlternativeSolutionCompletionPatches` trigger per that mechanism's own precedent) — no new
  infrastructure needed for this path.

Neither of these is re-derived or re-verified in this pass; they're restated here only so a future
implementer has the full picture in one document.

## 6. Consolidated fix list

| # | File (new unless noted) | Change |
|---|---|---|
| 1 | `DisableAllIssueBehaviorsExceptAllowlist.cs` (existing) | Add `typeof(EscortMerchantCaravanIssueBehavior)` to `Allowlist` |
| 2 | `EscortMerchantCaravanCaravanTalkConditionNullGuardPatch.cs` | Null-guard `caravan_talk_on_condition()` (§2.3, Option A) |
| 3 | `EscortMerchantCaravanOwnershipGatePatches.cs` | Ownership-gate `OnSettlementEntered`, `OnSettlementLeft`, `OnMapEventEnded`, `OnWarDeclared`, `OnClanChangedKingdom`, `OnPartyHourlyTick`, `OnSettlementOwnerChanged`, `HourlyTick`, `DailyTick`, `OnTimedOut` (§3.4/§4.2) |
| 4 | `EscortMerchantCaravanIssueQuest.InitializeQuestOnGameLoad` patch | Component-based `_questCaravanMobileParty` re-resolution fallback (§3.5) |
| 5 | New accept handler (name TBD, e.g. `EscortMerchantCaravanIssueHandler.cs`) | Broadcasts accept, calls `VillageNeedsToolsIssueOwnership.SetOwner`, force-writes `_companionRewardRandom`/`RewardGold` (§3.4 prerequisite, §5 first bullet) — **not designed in this pass**, blocking §3/§4's gates from having anything to gate against |
| 6 | Ownership registry persistence | Decide reuse of `VillageNeedsToolsIssueBehavior.SyncData` vs a new hook (§3.4 prerequisite) — **not resolved in this pass** |

## 7. Open items (explicit, not silently assumed solved)

1. Item 5/6 above (the accept handler and its persistence hook) are load-bearing prerequisites for §3/§4's
   ownership gates to do anything — without a real `SetOwner` call site, `IsLocalPeerOwner` is always false
   for every peer including the genuine owner, which would silently break the quest for everyone rather than
   just leave it unsynced. This must be designed and landed in the same pass as §3/§4, not after.
2. §3.5's fallback is included on a "costs nothing, don't assume unreachable" basis, not a proven-reachable
   one — needs a live join-mid-quest integration test once implemented to confirm whether it's ever actually
   exercised.
3. §4.4's documentation correction to the two other files is flagged, not fixed, here.

## 8. Go/No-Go

**Go.** Unlike the original design, every mechanism this document relies on is traced against real decompiled
source and this repo's own checked-out code (not assumed): the exact unsafe delegate (§2.2), the exact
call-chain proving `InitializeQuestOnLoadWithQuestManager` is genuinely live (§3.1), four additional concrete
unguarded dereferences beyond the dialogue conditions (§3.2), the real join-mid-quest save-transfer mechanism
(§3.1, cross-checked against `TransferSaveState.cs`/`CampaignState.cs`/`GameSaveDataPacketHandler.cs`), and
the corrected timeout mechanism (§4). The fix set (§6) is small, uses only patterns already proven elsewhere
in this codebase (narrow Harmony prefixes, the existing `VillageNeedsToolsIssueOwnership` registry, the
existing `InitializeQuestOnGameLoad` null/IsCaravan-check precedent), and needs no new architecture. The two
open items in §7 are real work, not hidden risk — item 1 in particular must land alongside §3/§4, not as a
follow-up, or the gates leave the quest permanently un-ownable. With that understood, this design is ready to
implement.
