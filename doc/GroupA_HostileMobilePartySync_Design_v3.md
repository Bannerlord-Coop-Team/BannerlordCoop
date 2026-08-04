# Group A — Shared Hostile-MobileParty Sync Infrastructure (Design v3, implementation-ready)

Branch: `feature/village-needs-tools-sync`. Supersedes v2 (workflow `ww4oc0pc3`) entirely — this is a
standalone document, not a diff against v2. Verified directly against the repo's checked-out state and
against decompiled vanilla source (`TaleWorlds.CampaignSystem.dll`) on 2026-08-03.

## 0. Scope correction (read first)

v2 covered 10 quest types. Four of them —
`LordsNeedsTutorIssueBehavior`, `LordWantsRivalCapturedIssueBehavior`, `RaidAnEnemyTerritoryIssueBehavior`,
`TheConquestOfSettlementIssueBehavior` — are **Tier 3** (war/settlement-ownership/prisoner-custody blast
radius) per this project's own tiering, not Tier 2 Group A. They drifted into v2's build order by mistake.
This design **excludes** them from Group A's build order. They already have their own, more careful,
Tier-3 design/critique track (see the project memory's Tier 3 section) and must go through that process,
not this one.

That leaves **6** genuinely-Tier-2 types in scope for Group A's build order:

- `GangLeaderNeedsWeaponsIssueQuestBehavior`
- `MerchantArmyOfPoachersIssueBehavior`
- `LandLordCompanyOfTroubleIssueBehavior`
- `SmugglersIssueBehavior`
- `CaravanAmbushIssueBehavior`
- `EscortMerchantCaravanIssueBehavior`

(Note: the source task brief for this revision said "8 quest types" in one place while separately excluding
4 of the original 10 named types — 10 − 4 = 6, matching the explicit list above and every other count in
this document. Treat "6" as authoritative; it's arithmetically consistent and matches the named list.)

`LordWantsRivalCapturedIssueBehavior` still gets one piece of work in this document (§2) because a real,
narrow bug was found in it during verification — but that work closes one gap in isolation and explicitly
does **not** clear the quest for implementation. See §2.2's standing caution.

## 1. Step 0 — allowlist state (verified, exact remaining work)

**Verified current repo state** (direct file reads, this session, `feature/village-needs-tools-sync`, clean
working tree):

- `source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs`'s `Allowlist`
  `HashSet<Type>` contains **none** of the 6 in-scope types (or the 4 excluded Tier-3 types). Read in full;
  the last entries are `HeadmanNeedsToDeliverAHerdIssueBehavior`, `ArtisanCantSellProductsAtAFairPriceIssueBehavior`,
  `GangLeaderNeedsToOffloadStolenGoodsIssueBehavior` (Tier 2 Group B, already shipped).
- Every one of the 9 orphaned `Disable<Type>IssueBehavior` patches for these 10 types **still exists**,
  unconditionally no-opping `RegisterEvents()` on its target — the same bug class documented in
  `VerifyAllowlistIntegrity()`'s own doc comment (already fixed 3× elsewhere in this codebase). Confirmed by
  direct read of all 9 files:

  | Type | Disable-patch file | In scope? |
  |---|---|---|
  | `GangLeaderNeedsWeaponsIssueQuestBehavior` | `source/GameInterface/Services/Towns/Patches/Disabled/DisableGangLeaderNeedsWeaponsIssueQuestBehavior.cs` | Yes |
  | `MerchantArmyOfPoachersIssueBehavior` | `source/GameInterface/Services/Towns/Patches/Disabled/DisableMerchantArmyOfPoachersIssueBehavior.cs` | Yes |
  | `LandLordCompanyOfTroubleIssueBehavior` | `source/GameInterface/Services/Villages/Patches/DisableLandLordCompanyOfTroubleIssueBehavior.cs` | Yes |
  | `CaravanAmbushIssueBehavior` | `source/GameInterface/Services/Caravans/Patches/DisableCaravanAmbushIssueBehavior.cs` | Yes |
  | `EscortMerchantCaravanIssueBehavior` | `source/GameInterface/Services/Caravans/Patches/DisableEscortMerchantCaravanIssueBehavior.cs` | Yes |
  | `LordsNeedsTutorIssueBehavior` | `source/GameInterface/Services/MobileParties/Patches/Disable/DisableLordsNeedsTutorIssueBehavior.cs` | No (Tier 3) |
  | `LordWantsRivalCapturedIssueBehavior` | `source/GameInterface/Services/MobileParties/Patches/Disable/DisableLordWantsRivalCapturedIssueBehavior.cs` | No (Tier 3) |
  | `RaidAnEnemyTerritoryIssueBehavior` | `source/GameInterface/Services/MobileParties/Patches/Disable/DisableRaidAnEnemyTerritoryIssueBehavior.cs` | No (Tier 3) |
  | `TheConquestOfSettlementIssueBehavior` | `source/GameInterface/Services/MobileParties/Patches/Disable/DisableTheConquestOfSettlementIssueBehavior.cs` | No (Tier 3) |

  All 9 are identical one-method Harmony shims: `[HarmonyPatch(typeof(X))] ... [HarmonyPatch(nameof(X.RegisterEvents))] static bool Prefix() => false;`

- `SmugglersIssueBehavior` has **no** matching disable patch anywhere in `GameInterface` (grepped the whole
  tree — zero hits). It just needs the allowlist add below, nothing to delete.

**Deletion of the 5 in-scope orphaned disable-patch files is explicitly out of scope for this document** —
per the task brief, a separate concurrent dead-code-sweep pass owns deleting orphaned disable patches
generically, and this design must not touch those files. This section only records their current state so
the allowlist step below isn't accidentally skipped on the (false) assumption that deleting the file alone
is sufficient. **It is not**: `IsAllowlisted()` and `VerifyAllowlistIntegrity()` both key off `Allowlist`
membership, not off whether a stray disable patch happens to exist. Even after the sweep deletes all 5
files, none of the 6 in-scope types will actually spawn until they're added to `Allowlist` — that add is
this design's job, not the sweep's, and doesn't depend on sweep timing.

**Exact remaining Step 0 work** (the one required code change this document assumes is landed before any of
§2–§4 below matters): add these 6 entries to `Allowlist` in `DisableAllIssueBehaviorsExceptAllowlist.cs`:

```csharp
// Tier 2 Group A (see doc/GroupA_HostileMobilePartySync_Design_v3.md)
typeof(GangLeaderNeedsWeaponsIssueQuestBehavior),
typeof(MerchantArmyOfPoachersIssueBehavior),
typeof(LandLordCompanyOfTroubleIssueBehavior),
typeof(SmugglersIssueBehavior),
typeof(CaravanAmbushIssueBehavior),
typeof(EscortMerchantCaravanIssueBehavior),
```

Do **not** add `LordsNeedsTutorIssueBehavior`, `LordWantsRivalCapturedIssueBehavior`,
`RaidAnEnemyTerritoryIssueBehavior`, or `TheConquestOfSettlementIssueBehavior` here — they stay disabled
(orphaned patch or not) until their own Tier 3 track clears them. If the concurrent sweep deletes those 4
types' orphaned disable patches too (plausible, since it's a generic sweep), that's fine and orthogonal —
without an `Allowlist` entry they remain inert (`DisablePrefix`-style blocking simply becomes unnecessary
rather than load-bearing), so no re-adding a disable patch is needed for them either.

This allowlist edit and the concurrent sweep's file deletions both touch
`DisableAllIssueBehaviorsExceptAllowlist.cs`'s deletions vs. this file's `Allowlist` array respectively —
they're different regions of the same file family but not necessarily the same file for the deletions
(the disable patches live in their own topical files, e.g. `DisableGangLeaderNeedsWeaponsIssueQuestBehavior.cs`).
The only actual same-file collision risk is if the sweep pass also touches
`DisableAllIssueBehaviorsExceptAllowlist.cs` itself (it shouldn't need to — that file has no orphaned-patch
shape itself, it's the allowlist mechanism, not a target of it). Land the allowlist add as its own commit
regardless of sweep timing.

## 2. Ownership-gate patches (required)

Full verification (this session) traced every party-spawn and `MobileParty.MainParty`/`PartyBase.MainParty`
read site in all 6 in-scope types against decompiled vanilla source. Result, unchanged from the prior
verification pass: **zero of them need `MainPartyReadRedirectTranspiler`/`QuestReferencePartyResolver`
work.** See §5 for why that entire mechanism is dropped. Two real, narrow ownership gaps were found instead.

### 2.1 `GangLeaderNeedsWeaponsIssueQuestBehavior` — weapon-delivery dialogue has no ownership check

**File**: new `source/GameInterface/Services/Issues/Patches/GangLeaderNeedsWeaponsQuestOwnershipGatePatch.cs`

**Vanilla mechanism** (`GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest.SetDialogs()`,
confirmed by decompile):

```csharp
DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss")
    .NpcLine(...)
    .Condition(() => Hero.OneToOneConversationHero == base.QuestGiver)   // ANY peer talking to the giver
    .BeginPlayerOptions()
    .PlayerOption(new TextObject("...Here is your cargo..."))
    .Condition(CheckIfPlayerHasEnoughRequestedWeapons)                  // checks the TALKER's own PartyBase.MainParty.ItemRoster
    .NpcLine(...)
    .Consequence(delegate { Campaign.Current.ConversationManager.ConversationEndOneShot += PlayerSuccessfullyDeliveredWeapons; })
    .CloseDialog()
    ...
```

`CheckIfPlayerHasEnoughRequestedWeapons()` (private, `bool`, no args) recomputes
`_collectedItemAmount` from **whichever local machine's own** `PartyBase.MainParty.ItemRoster` is currently
running this dialogue, with no ownership check anywhere in the chain. Since `quest_discuss`'s own condition
only checks `Hero.OneToOneConversationHero == base.QuestGiver` (the shared gang-leader NPC, not
owner-specific), **any connected peer who walks up to that NPC carrying ≥ `_requestedWeaponAmount` of the
requested `WeaponClass` can complete and collect someone else's already-accepted quest.**

Every other listener/dialogue in this quest (`OnSettlementEnter`/`OnSettlementLeft` gated on
`party == MobileParty.MainParty`, the `_guardsParty` persuasion/bribe/fight flow gated behind `_guardsParty`
only existing on the accepter's own encounter) is already self-gated and confirmed clean — this is the one
real gap.

**Fix**: gate the condition method itself, exactly like the existing
`VillageNeedsToolsQuestOwnershipGatePatch` precedent (same shape: a `PlayerOption.Condition` gate is
sufficient on its own here because `PlayerSuccessfullyDeliveredWeapons` is only ever reached through this
one gated option — no proactive/out-of-band path exists for this quest, unlike
`GangLeaderNeedsToOffloadStolenGoods`'s `OnSettlementLeft` case, so no second defense-in-depth gate is
needed):

```csharp
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see VillageNeedsToolsQuestOwnershipGatePatch's doc comment for the derivation):
/// GangLeaderNeedsWeaponsIssueQuest's quest_discuss dialogue gates its "Here is your cargo" option on
/// CheckIfPlayerHasEnoughRequestedWeapons(), which checks whichever peer is CURRENTLY TALKING's own
/// PartyBase.MainParty.ItemRoster with no ownership check — any non-owner carrying enough of the requested
/// WeaponClass can complete and collect someone else's quest. Gating this one condition method is
/// sufficient: PlayerSuccessfullyDeliveredWeapons is only ever reached via this option's Consequence
/// delegate, with no proactive/out-of-band trigger (unlike GangLeaderNeedsToOffloadStolenGoods'
/// OnSettlementLeft), so no second defense-in-depth gate is required here.
/// </summary>
[HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest))]
internal class GangLeaderNeedsWeaponsQuestOwnershipGatePatch
{
    [HarmonyPatch("CheckIfPlayerHasEnoughRequestedWeapons")]
    [HarmonyPrefix]
    private static bool CheckIfPlayerHasEnoughRequestedWeaponsPrefix(
        GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance, ref bool __result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false; // skip the original — non-owners never see the option become selectable
        }

        return true; // real owner: run the real check unmodified
    }
}
```

### 2.2 `LordWantsRivalCapturedIssueBehavior` — capture/delivery chain has no ownership check (NOT a clearance to implement)

**Standing caution, repeated so this section can't be read out of context**: this quest has separately
documented, more severe war-declaration and prisoner-custody dangers from independent Tier-3 critiques
(`OnHeroKilled` firing on every mirror since `RegisterEvents()` re-fires on every campaign load;
`DeclareWarAction.ApplyInternal` dereferencing `Hero.MainHero`, null on a dedicated server;
`CompleteQuestWithSuccess()`'s finalize cascade running before the delivery-validation point it needs to
gate; `TakePrisonerActionPatches`/`TransferPrisonerAction` non-host-client forwarding gaps). Project policy
requires **explicit user sign-off** before this quest is ever implemented, independent of anything below.
**This subsection closes one gap for design completeness only — it does not clear
`LordWantsRivalCapturedIssueBehavior` for implementation, and it is excluded from Group A's build order
(§6).**

**Vanilla mechanism** (`LordWantsRivalCapturedIssueQuest`, confirmed by decompile): the capture/delivery
reward is reachable through **three** separate dialogue entry points, all converging on two completion
methods:

1. `GetTargetHeroDialogFlow()` — talking to the wandering `_targetHero` directly. Gated by
   `target_hero_encounter_default_condition`/`target_hero_encounter_agressive_condition`, both of which call
   `common_first_dialogue_condition()`:
   ```csharp
   private bool common_first_dialogue_condition()
   {
       if (Hero.OneToOneConversationHero == _targetHero && _targetHero.CurrentSettlement == null && !PlayerCapturedTargetHero)
           return Campaign.Current.CurrentConversationContext != ConversationContext.CapturedLord;
       return false;
   }
   ```
   No ownership check — reachable by **any** peer who independently encounters the same `_targetHero`
   object (shared/synced across peers).
2. `DiscussDialogFlow`'s `quest_discuss` — "Yes, I've captured them" option, gated only on
   `Hero.OneToOneConversationHero == base.QuestGiver` (the shared quest-giver NPC) and
   `PlayerCapturedTargetHero` (checks the **talker's own** `MobileParty.MainParty.PrisonRoster`). Consequence
   subscribes `PlayerDeliveredPrisonerQuestSuccess`.
3. `GetQuestGiversAgentDialogFlow()` — reached via `OnSettlementEntered` when `party == MobileParty.MainParty
   && hero == Hero.MainHero` (self-gated to the local peer's own party) while `PlayerCapturedTargetHero` is
   true — but `PlayerCapturedTargetHero` itself has no ownership check, so any peer who happens to be
   independently holding `_targetHero` prisoner (e.g. captured them in an unrelated battle) and walks into a
   fortification owned by `base.QuestGiver.Clan` triggers this too.

All three paths converge on exactly two named, private completion methods:
`PlayerDeliveredPrisonerQuestSuccess()` (success — gold, relation, `CompleteQuestWithSuccess()`,
`TransferPrisonerAction`) and `QuestFailCounterOfferAccepted()` (target bribes their way free —
`CompleteQuestWithBetrayal()`, `EndCaptivityAction`). **This is a better fix point than gating
`GetTargetHeroDialogFlow`'s own condition alone** (which the raw finding that seeded this design pointed
at) — gating only that one entry point would leave paths 2 and 3 open, exactly the "gate the actual
mutation, not each registration/condition site" principle this codebase already uses for
`GangLeaderNeedsToOffloadStolenGoods`.

**Fix**: gate the two completion methods.

```csharp
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// NOT a clearance to implement LordWantsRivalCapturedIssueBehavior — see the Tier 3 sign-off requirement
/// documented in doc/GroupA_HostileMobilePartySync_Design_v3.md §2.2. This closes one isolated gap only:
/// the capture/delivery reward is reachable through three separate dialogue entry points
/// (GetTargetHeroDialogFlow's encounter condition, quest_discuss's "I've captured them" option, and the
/// quest-giver's-agent dialogue), none of which check ownership, all of which converge on
/// PlayerDeliveredPrisonerQuestSuccess/QuestFailCounterOfferAccepted. Gating those two completion methods
/// (rather than each of the three entry conditions individually) closes all three paths in one place —
/// matching the precedent in GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches' doc comment.
/// </summary>
[HarmonyPatch(typeof(LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest))]
internal class LordWantsRivalCapturedOwnershipGatePatches
{
    [HarmonyPatch("PlayerDeliveredPrisonerQuestSuccess")]
    [HarmonyPrefix]
    private static bool PlayerDeliveredPrisonerQuestSuccessPrefix(
        LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestFailCounterOfferAccepted")]
    [HarmonyPrefix]
    private static bool QuestFailCounterOfferAcceptedPrefix(
        LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
```

`FirstCounterOfferFinished()` (re-applies `TakePrisonerAction`, sets `_firstCounterOfferMade`) is
deliberately **not** gated — it grants no reward and completes nothing; it's harmless on a non-owner's own
local mirror, same reasoning already established elsewhere in this codebase for non-mutating local state.

## 3. Optional defensive `Instance`-resolution patches (low priority, not required)

Both types below have a menu-driven callback that reads a private static `Instance` getter shaped exactly
like `BettingFraudIssueBehavior`'s (a `_cachedQuest` field + "first ongoing match in
`Campaign.Current.QuestManager.Quests`" fallback). Verified: this is **not** a cross-peer exploit — per §5,
a non-owner's mirrored quest object never calls `StartQuest()`, so it's never inserted into
`QuestManager.Quests` on that peer's own machine, so the "first found" scan can only ever find quests this
same local player genuinely started. The real (minor) risk is the **same local player running two
concurrent quests of this exact type** (from two different NPCs) picking the wrong one — an existing
vanilla ambiguity, not introduced by this mod, and already accepted precedent
(`BettingFraudInstanceResolutionPatch.cs`) for hardening it defensively.

### 3.1 `MerchantArmyOfPoachersIssueBehavior`

`engage_poachers_consequence`/`army_of_poachers_village_on_init` call the static `Instance` getter. Unlike
Betting Fraud, this quest has a location field (`_questVillage`) that can disambiguate directly instead of
falling back to ownership:

```csharp
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Optional hardening, same shape as BettingFraudInstanceResolutionPatch.cs. Not a cross-peer exploit —
/// see §3 of doc/GroupA_HostileMobilePartySync_Design_v3.md — just a same-local-player two-concurrent-quests
/// disambiguation improvement over vanilla's "first found" cache. Resolves by matching _questVillage's
/// settlement against the local player's current settlement/encounter context (the same field
/// army_of_poachers_village_on_init and HourlyTick already key off of), falling back to IsLocalPeerOwner
/// and then first-found to preserve vanilla's original permissiveness if nothing disambiguates.
/// </summary>
[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior), "Instance", MethodType.Getter)]
internal class MerchantArmyOfPoachersInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var currentSettlement = Settlement.CurrentSettlement ?? PlayerEncounter.EncounterSettlement;
        MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest fallback = null;

        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest candidate || !candidate.IsOngoing)
                continue;

            fallback ??= candidate;
            if (currentSettlement != null && candidate._questVillage.Settlement == currentSettlement)
            {
                __result = candidate;
                return false;
            }
        }

        __result = fallback;
        return false;
    }
}
```

### 3.2 `LandLordCompanyOfTroubleIssueBehavior`

`company_of_trouble_menu_on_init` calls the static `Instance` getter. This quest has no location field (the
"company" rides in `MobileParty.MainParty`'s own roster), so the disambiguator instead is: prefer whichever
ongoing candidate currently has one of its own transient menu-trigger flags set — those are only ever set by
that candidate's own `HourlyTick`, which (per §5) only ever runs on its true owner's machine for its own
quest instance.

```csharp
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Optional hardening, same shape and rationale as MerchantArmyOfPoachersInstanceResolutionPatch. No
/// location field exists here, so disambiguation instead prefers whichever ongoing candidate currently has
/// one of its own transient menu-trigger flags set (_checkForBattleResults/_triggerCompanyOfTroubleConversation/
/// _battleWillStart/_companyLeftQuestWillFail) — those are only ever set by that candidate's own HourlyTick,
/// which only runs for its true local owner. Falls back to first-ongoing to preserve vanilla's original
/// permissiveness if nothing disambiguates.
/// </summary>
[HarmonyPatch(typeof(LandLordCompanyOfTroubleIssueBehavior), "Instance", MethodType.Getter)]
internal class LandLordCompanyOfTroubleInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest fallback = null;

        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is not LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest candidate || !candidate.IsOngoing)
                continue;

            fallback ??= candidate;
            if (candidate._checkForBattleResults || candidate._triggerCompanyOfTroubleConversation ||
                candidate._battleWillStart || candidate._companyLeftQuestWillFail)
            {
                __result = candidate;
                return false;
            }
        }

        __result = fallback;
        return false;
    }
}
```

Both patches follow `BettingFraudInstanceResolutionPatch.cs`'s `CallOriginalPolicy.IsOriginalAllowed()`
gate exactly. **Optional — do not block Group A's build order on these two.**

## 4. Roster / `OwnerParty`-ordering checklist (re-scoped to the 6 in-scope types, carried forward)

v2's concern: a custom-spawned `MobileParty`'s `TroopRoster` registers itself for network sync from
`TroopRoster`'s `set_OwnerParty` (see `TroopRosterOwnerPartyRegistrationPatch.cs`'s own doc comment — the
`TroopRoster(PartyBase)` ctor gets JIT-inlined into every caller, including `PartyBase..ctor`, so
`set_OwnerParty` is the one reliable, non-inlined chokepoint). Troops added to a roster **before**
`OwnerParty` is set on it would silently never register for sync.

Verified directly against decompiled source for the three types with a bespoke spawn method (the other 3 —
`SmugglersIssueBehavior`, `CaravanAmbushIssueBehavior`, `EscortMerchantCaravanIssueBehavior` — were not
re-read line-by-line this pass; carrying forward v2/prior verification's "confirmed safe" conclusion for
them since they use the same factory-method convention pervasive throughout this codebase — treat as a
cheap spot-check at implementation time, not a redesign item):

- **`GangLeaderNeedsWeaponsIssueQuestBehavior.CreateGuardsParty()`**: `_guardsParty =
  CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)` (factory returns a fully-constructed
  `MobileParty` — `OwnerParty` already set via `PartyBase..ctor` internally) **then**
  `_guardsParty.MemberRoster.AddToCounts(...)` twice. Safe order.
- **`MerchantArmyOfPoachersIssueBehavior.CreatePoachersParty()`**: same shape —
  `CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)` returns the constructed party, **then**
  `_poachersParty.ItemRoster.AddToCounts(...)`/`.MemberRoster.AddToCounts(...)`. Safe order.
- **`LandLordCompanyOfTroubleIssueBehavior`**: the "company" itself is added straight to
  `MobileParty.MainParty.MemberRoster` (already long-constructed, trivially safe). Its actual spawned
  hostile party, `_companyOfTroubleParty`, is built via `BanditPartyComponent.CreateBanditParty(...)`
  (factory returns constructed party) **then** `_companyOfTroubleParty.MemberRoster.AddToCounts(...)`. Safe
  order.

No fix needed for any of the 3 directly re-verified types. Carry forward "confirmed safe" for the other 3.

## 5. Section dropped: `MainPartyReadRedirectTranspiler` / `QuestReferencePartyResolver`

v2 gated a large chunk of work behind a PoC milestone for redirecting `MobileParty.MainParty`/
`PartyBase.MainParty` reads inside quest code to "whoever accepted this quest" instead of "this machine's
own party." **This entire mechanism is not needed and should not be built.**

Direct verification (this session, cross-checked against decompiled source for all 6 in-scope types plus
`LordWantsRivalCapturedIssueBehavior`): every `MainParty` read site in every one of these types falls into
one of two categories, and both are already correct without any redirect:

- **Category A — tick/listener methods.** `RegisterEvents()`'s listeners (`OnSettlementEnter`,
  `HourlyTick`, `MapEventEnded`, etc.) only ever fire meaningfully on a machine where the quest is
  `IsOngoing` — and `IsOngoing` is only ever set by `QuestBase.StartQuest()`, which is only ever called from
  a live dialogue `Consequence` (`QuestAcceptedConsequences` in every type here) — i.e., only on the genuine
  accepter's own machine. `IssueBase.StartIssueWithQuest()`, the entry point every non-owner's mirror-replay
  path uses to construct its own copy of the Quest object, **never calls `StartQuest()`**. So on a
  non-owner's own machine, these listeners are wired (the constructor still runs `RegisterEvents()` in some
  types — see `InitializeQuestOnGameLoad()`) but the quest's own tick/callback logic that actually reads
  `MainParty` never meaningfully executes, because the state it depends on (`_guardsParty`, `_poachersParty`,
  `_companyOfTroubleParty`, `_questCaravanMobileParty`, etc.) is never populated outside the accepter-only
  path either. `MainParty` on this category already correctly means "this machine's own party" wherever it
  does execute, because it only executes on the one machine where that's the right meaning.
- **Category B — dialogue consequences.** `OfferDialogFlow`/`DiscussDialogFlow` consequences (e.g.
  `QuestAcceptedConsequences`, `PlayerSuccessfullyDeliveredWeapons`) only ever run on the machine whose own
  live conversation reached them — again, `MainParty` already correctly means that machine's own party.

Confirmed via the same reasoning this codebase already documents explicitly in
`BettingFraudInstanceResolutionPatch.cs` and `LordNeedsGarrisonTroopsInstanceResolutionPatch.cs`. No call
site in any of the 7 types checked this session needs redirecting to "the accepter's data" instead of
"whoever's machine this is" — that need simply doesn't occur. Drop the transpiler section from the design
entirely; do not schedule it as future work.

## 6. Scripted-battle pipeline (re-scoped to the 6 in-scope types)

Exactly 3 of the 6 in-scope types open a scripted local battle mission directly from quest code rather than
going through this repo's existing, audited, server-arbitrated mission-open pipeline
(`BattleMissionStartHandler` → `NetworkBattleStartRequest`/`NetworkStartAttackMission` →
`BattleMissionEntryPatch` on `CampaignMission.OpenBattleMission(MissionInitializerRecord)` →
`ICoopFieldBattleLauncher`). Confirmed by decompile which vanilla overload each one calls, and confirmed
against `BattleMissionEntryPatch`'s own `[HarmonyPatch]` attribute which overload it actually covers:

| Type | Trigger method | Vanilla call | Hits `BattleMissionEntryPatch`? |
|---|---|---|---|
| `GangLeaderNeedsWeaponsIssueQuestBehavior` | `StartFight()` | `CampaignMission.OpenBattleMissionWhileEnteringSettlement(string, int, int, int)` | **No** — different method entirely |
| `MerchantArmyOfPoachersIssueBehavior` | `StartQuestBattle()` | `CampaignMission.OpenBattleMission(string scene, bool usesTownDecalAtlas, string sceneLevels = "")` | **No** — different overload |
| `LandLordCompanyOfTroubleIssueBehavior` | `company_of_trouble_menu_on_init`'s `_battleWillStart` branch | `CampaignMission.OpenBattleMission(string scene, bool usesTownDecalAtlas)` | **No** — same overload as above, still not the patched one |

`BattleMissionEntryPatch` is pinned to exactly one overload:
`[HarmonyPatch(typeof(CampaignMission), nameof(CampaignMission.OpenBattleMission), new[] { typeof(MissionInitializerRecord) })]`
— its own doc comment explains why (`AccessTools.Method` is ambiguous across overloads without the explicit
signature). All three of these quests' own scripted battles miss it entirely: no `BattleSpawnGate.BeginBattle`,
no `ICoopBattleBehaviorAttacher.Attach`, no `PlayerEnteredBattle` publish. **This is a confirmed, concrete gap
across all 3 types** — narrower and more precisely diagnosed than v2's "unresolved question," now resolved:
it's not a question of routing through a different pipeline vs. not, it's that these calls silently miss the
existing multiplayer-safety patch by virtue of using a different vanilla method signature. The `MapEvent`
these quests build first (via `PlayerEncounter.RestartPlayerEncounter`/`.Start()` + `SetupFields()` +
`StartBattle()`) is itself local-only and accepter-gated (§5, Category A/B) — that part is fine and doesn't
need the server-arbitrated `NetworkBattleStartRequest` round-trip these are inherently private,
quest-scoped 1-vs-NPC-party encounters, not shared MobileParty-vs-MobileParty battles other peers need to
join. The gap is specifically the missing coop-mission-attach step.

**Two options, same as v2's conclusion, now with concrete method signatures:**

- **Option 1 — harmonize the call site.** Change each quest's trigger method to build a proper
  `MissionInitializerRecord` (mirroring what `IBattleMissionInitializerResolver.Create(...)` already does
  for the audited pipeline) and call the one overload `BattleMissionEntryPatch` already covers, instead of
  the settlement-entry/scene-string overloads. Cheapest change, but requires faithfully reproducing each
  quest's bespoke scene/`SceneLevels`/wall-level/decal-atlas parameters as `MissionInitializerRecord` fields
  — a place to introduce a subtle regression in vanilla's own scene-selection logic if not done carefully.
- **Option 2 (recommended) — extend the patch's target list.** Extract `BattleMissionEntryPatch`'s existing
  `Prefix`/`Postfix` bodies into two shared internal static methods (e.g.
  `BattleMissionEntryShared.EngageSpawnGate()` / `.AttachAndPublish()`), have the existing patch class call
  them, and add two more small patch classes targeting the other two overloads with trivial
  `Prefix`/`Postfix` wrappers that call the same shared methods:

  ```csharp
  [HarmonyPatch(typeof(CampaignMission), nameof(CampaignMission.OpenBattleMissionWhileEnteringSettlement))]
  internal class BattleMissionEntryPatch_EnteringSettlement
  {
      [HarmonyPrefix] private static void Prefix() => BattleMissionEntryShared.EngageSpawnGate();
      [HarmonyPostfix] private static void Postfix() => BattleMissionEntryShared.AttachAndPublish();
  }

  [HarmonyPatch(typeof(CampaignMission), nameof(CampaignMission.OpenBattleMission),
      new[] { typeof(string), typeof(bool), typeof(string) })]
  internal class BattleMissionEntryPatch_SceneOverload
  {
      [HarmonyPrefix] private static void Prefix() => BattleMissionEntryShared.EngageSpawnGate();
      [HarmonyPostfix] private static void Postfix() => BattleMissionEntryShared.AttachAndPublish();
  }
  ```

  Zero changes to any of the 3 quests' own scene-selection code (lower regression risk on vanilla numeric
  detail), at the cost of one more overload to remember to extend if a future not-yet-audited quest type
  uses yet another `OpenBattleMission*` overload. **This is the recommended option** — it keeps the fix
  entirely inside already-multiplayer-aware infrastructure code instead of touching 3 separate quest
  classes' scene-selection logic.

Do not build both; pick one per implementer/reviewer judgment at implementation time. Recommendation stands:
Option 2.

## 7. Section carried forward by reference: dead-file citation fix

v2 (per this revision's task brief) included a small "Section 4" fixing a stale file citation somewhere in
its own text. No persisted copy of v2's raw document was found in the repo or in project memory this
session (it only ever existed as prior workflow output, `ww4oc0pc3`/`wfv5evyxt`) — the specific citation it
corrected could not be reconstructed for this document. This is flagged rather than guessed at: if v2's raw
output resurfaces, port that one fix forward; otherwise treat it as superseded — this document's own
citations (file paths, method names) were independently re-verified against the current repo and decompiled
source this session, so nothing here depends on that unresolved citation.

## 8. Build order and go/no-go

**Go**, for the 6 in-scope types, once §1's allowlist add lands (waiting on nothing else — the concurrent
dead-code sweep is orthogonal, not a blocker).

1. **Step 0** (§1): allowlist add for the 6 in-scope types. No functional risk — mechanical, same shape as
   Group B's own Step 0.
2. **`SmugglersIssueBehavior`** — no orphaned disable patch to worry about, no ownership gap found, no
   scripted-battle-pipeline gap, roster ordering not independently re-verified this pass but no bespoke
   custom-party spawn method was flagged by any prior pass either. Simplest type in the group; build first
   to validate the allowlist add end-to-end with real gameplay before touching anything with a gap to fix.
3. **`CaravanAmbushIssueBehavior`** — same clean bill of health as Smugglers. Build second.
4. **`GangLeaderNeedsWeaponsIssueQuestBehavior`** — needs §2.1's ownership gate + §6's battle-pipeline fix
   (Option 2 recommended). Build third; smallest of the three quests needing both fixes.
5. **`MerchantArmyOfPoachersIssueBehavior`** — needs §6's battle-pipeline fix + §3.1's optional
   `Instance`-resolution hardening (do it now since you're already touching `BattleMissionEntryPatch`-adjacent
   code for #4). Build fourth.
6. **`LandLordCompanyOfTroubleIssueBehavior`** — needs §6's battle-pipeline fix (shares the same
   `BattleMissionEntryPatch_SceneOverload` target added in step 4/5 — no new Harmony patch needed here, just
   the type's own allowlist coverage) + §3.2's optional `Instance`-resolution hardening. Build fifth.
7. **`EscortMerchantCaravanIssueBehavior`** — per the project's own roadmap this is materially bigger
   (~2.5–4× effort: repeated in-quest RNG, a real conflict with `IssueManagerTickPatches`' server-only
   `HourlyTick` gate that assumes every issue's own tick is a no-op, which this one's isn't). No gap found
   in this session's verification pass, but budget it as its own milestone, not a same-sized unit with the
   other 5. Build last in Group A, after the other 5 are shipped and tested — do not let its bigger size
   block the other 5 from landing first.

Each type gets real E2E test coverage per this project's standing practice (`How to apply` in project
memory: design → adversarial critique → implement with tests → independent review that reverts each claimed
fix and confirms the specific test fails, then restores). For steps 4–6 specifically, the review pass must
include reverting the ownership-gate prefix and confirming a non-owner-completes-the-quest test actually
fails without it — not just re-reading the patch and assuming it works, per this project's own established
verification discipline.

`LordWantsRivalCapturedIssueBehavior`, `LordsNeedsTutorIssueBehavior`, `RaidAnEnemyTerritoryIssueBehavior`,
`TheConquestOfSettlementIssueBehavior`: **no-go for this design's build order** (§0). §2.2's fix is recorded
for whenever Tier 3's own process reaches this quest, but nothing in this document authorizes starting that
work now.
