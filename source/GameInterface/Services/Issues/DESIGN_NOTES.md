# Issues subsystem — design notes

Non-obvious constraints and prior-incident context for the Issues quest-sync subsystem, kept out of
inline comments per the zero-comments code policy. `feature/generic-quest-handler` (#2763) is
framework-only — every individual quest type (`Migrated/<QuestName>/...`) lives on its own
`feature/quest-*` branch, each carrying its own full copy of this file with that type's own notes
added. Only genuinely shared-framework notes belong here; a quest-specific note added on a quest
branch should stay there, not get backported onto this one.

## AlternativeSolutionReturnTimeForTroops — do not pin to a sentinel value

An earlier design pinned `AlternativeSolutionReturnTimeForTroops` to `CampaignTime.Never` on non-owner
mirrors. This was reverted: `Coop.Core.Server.Connections.States.TransferSaveState` reships the server's
own live campaign state (a full save snapshot) to any (re)connecting peer. If the server's own copy of
this field were pinned to a wrong sentinel, that wrong value would get reshipped and permanently cemented
on every future reconnect — a worse, permanent soft-lock (troops that can never return) than the bug being
fixed. The field must always hold the real, correctly-synced value (via `AlternativeSolutionVanillaStateSync`),
never a placeholder — reshipping accurate state on reconnect is correct behavior, not a bug.

Relevant files: `Patches/VillageNeedsToolsAlternativeSolutionCompletionPatches.cs`,
`Generic/AcceptMirror/IAlternativeAcceptMirrorStrategy.cs`, `Interfaces/GenericAcceptMirrorInterface.cs`.

## AlternativeSolutionVanillaStateSync — why it captures more than the bare state flag

Vanilla's `StartIssueWithAlternativeSolution()` sets more than the bare `_issueState`/`IsTriedToSolveBefore`
transition: real return/effect-clear times, failure/casualty rolls, companion reward skill, troop XP pool,
and a matching journal log entry. `Capture` reads these off whichever peer's genuine call produced them;
`Apply` force-writes them on every other peer. Letting a second peer independently recompute failure/casualty
via its own RNG would desync any instance using `AlternativeSolutionScaleFlag.FailureRisk`/`Casualties`
(the two currently-migrated types are Duration-only/None, so this is presently a no-op for them, but the
capture is generic so a future flagged type stays correct).

`Apply`'s journal-log step reads `AlternativeSolutionHero` (computed from `AlternativeSolutionSentTroops`),
so the real troop roster must be applied to the receiving peer *before* `Apply` runs, or the vanilla getter
NREs on a null companion. Every accept-mirror handler (`GenericQuestTypeAcceptHandler`,
`GenericAcceptMirrorHandler`) applies received troops before invoking the state mirror for this reason.

## Reflection that can't be converted to direct field/property access

Krafs.Publicizer makes `TaleWorlds.CampaignSystem`'s private/internal members directly dot-accessible at
compile time, so most reflection in this subsystem was converted to direct access. Three categories remain
reflection-only, confirmed by decompile/compiler error, not by assumption:
- `IssueBase.AlternativeSolutionStartLog` / `AlternativeSolutionBaseDurationInDaysInternal` — `protected`
  properties; Publicizer only lifts `private`/`internal` to `public`, not `protected`.
- `VillageNeedsCraftingMaterialsIssueQuest._requestedItemAmount`, `QuestBase.RewardGold`,
  `JournalLog.Range` — genuine `readonly` vanilla fields; direct assignment can't bypass `readonly`
  (confirmed via CS0191), only reflection's `FieldInfo.SetValue` can.

## RejectAcceptance has two genuinely different correct implementations

`AcceptMirrorSupport.RejectAcceptance` (checks `IssueOwnershipRegistry.IsLocalPeerOwner`, i.e. "am I *not*
the recorded owner") and `VillageNeedsCraftingMaterialsQuestType.RejectAcceptanceCore` (checks
`IssueOwnershipRegistry.TryGetOwnerControllerId`, i.e. "is *anyone* recorded yet") look like the same bug
in two shapes — but they're both correct, for two different accept-mirror architectures:

- **VillageNeedsTools' mirror (`GenericAcceptMirrorInterface.MirrorQuestAccepted`) never touches `IssueQuest`**
  — it only flips a state enum. A non-owner peer's `IssueQuest` stays `null` forever unless that peer itself
  raced a genuine local accept and created one. `IssueQuest != null` on a non-owner peer can therefore only
  mean "this is my own stray quest from a lost race" — so `IsLocalPeerOwner` (cancel unless I'm the real
  owner) is the correct check.
- **VillageNeedsCraftingMaterials' mirror (`QuestSolutionAcceptMirrorStrategy.MirrorQuestAccepted`) adopts/
  corrects whatever `IssueQuest` object exists** — it calls `StartIssueQuest` if none exists yet, then
  force-writes the winner's real field values onto it regardless. Given the verified per-peer reliable-ordered
  transport guarantees the winner's broadcast is always processed before my own rejection, by the time my
  own `RejectAcceptance` runs, `IssueQuest` is *already* the winner's correctly-mirrored quest, not a stray
  one — `IsLocalPeerOwner` would incorrectly cancel it. `TryGetOwnerControllerId` (cancel only if literally
  no one is recorded yet) is the correct check here, because the mirror's own force-write already handles
  convergence; there is nothing left for reject to clean up once any owner is recorded.

Do not "fix" one to match the other without re-deriving which mirror shape it backs — this was gotten wrong
once already (mid-session, applying `AcceptMirrorSupport.RejectAcceptance` to CraftingMaterials broke
`RequestQuestTypeAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer`, caught by
the existing test suite and reverted).

## Deposit/Drain identity derivation

`AwaitingAlternativeSolutionTroopsHandler`'s Deposit/Drain requests carry no client-supplied identity
field. The owner is always derived server-side from the authenticated connection (`payload.Who`), never
trusted from a client-claimed `ControllerId` string — tracing the vanilla trigger points
(`IssueManagerAlternativeSolutionTroopsPatches.cs`) confirms Deposit/Drain only ever legitimately concern
the sender's own bucket, so there's nothing for a client to correctly claim on anyone else's behalf; a
claimed identity field would only be an attack surface, never a legitimate use case.

## GenericAcceptMirrorHandler — accept-request identity and ordering

- Identity for `RequestGenericIssueAcceptQuest`/`RequestGenericIssueAcceptAlternative` is derived
  server-side from the authenticated `NetPeer`, never trusted from a client-claimed value — a client has
  no field in these messages to put a spoofed `ControllerId` into.
- In `Handle_NetworkGenericIssueQuestAccepted`, the mirror call and `IssueOwnershipRegistry.SetOwner` run
  in the same synchronous block with no yield between them, so no other message can interleave and observe
  a "mirrored but ownership unknown" state.

## AwaitingAlternativeSolutionTroopsTests — no quest-type production code required

`feature/generic-quest-handler` (#2763) carries only the shared framework — no `VillageNeedsTools`/
`VillageNeedsCraftingMaterials` Handler/Messages/QuestType files (those belong to their own PRs). This
test still needs a real, concrete `IssueBase` on both peers to exercise the shared
`AwaitingAlternativeSolutionTroopsRegistry`/`IssueOwnershipRegistry` end-to-end, so `CreateIssueOnBothPeers`
constructs the vanilla `VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue` directly on each peer's own
`Campaign.Current.IssueManager`, instead of creating it once on the server and relying on a creation-
replication `Handler.cs` (which is inherently per-quest-type and doesn't exist on this branch) to mirror it
to the client. `VillageNeedsToolsIssueBehavior` is a stock TaleWorlds class, not something either PR ships.

`CreateIssueOnBothPeers` guards its direct construction with `owner.Issue == null` before calling
`CreateNewIssue`. This matters once a quest type's own `QuestTypeDescriptor` for
`VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue` is stacked on top of this branch (e.g. Tools' own
PR): `GenericQuestTypeCreationTriggerPatch.Postfix` fires for *any* genuine creation success regardless of
which code caused it, so this test's own direct construction on the server also triggers that quest type's
real `OnGenuineCreation` → broadcast → the client's queued network handler, which can win the race and
populate `owner.Issue` before this test's own client-side construction runs — vanilla's `CreateNewIssue`
throws `ArgumentException` on the second insert for the same Hero. The guard makes the test agree with
whichever path gets there first rather than assuming it's always this test's own construction.

## SimpleIssueFactoryRegistry — which vanilla Issue types qualify

Backs `SimpleIssueCreationPatch`/`SimpleIssueCreationHandler` for Issue types whose class can be reconstructed
from just the owner `Hero` — rolling no field a client would need captured/forced to replicate byte-identically.
Still needs its own server-authoritative-creation-broadcast-then-replicate flow:
`IssueManagerCreateNewIssuePatches.Prefix` unconditionally blocks every `IssueManager.CreateNewIssue` call on a
client, so without this a client would never receive one of these issues at all. A type NOT in this registry
rolls or references at least one field (at creation, or for The Spy Party, accept time) that needs
capturing+forcing rather than being safely re-derivable from the owner alone, so it keeps its own bespoke
Interface/Messages/Patches/Handler file set instead.

Per-entry notes on why specific types qualify (each confirmed against the decompiled source):
- `LandLordTheArtOfTheTradeIssue`'s extra `ItemObject` ctor param is never rolled — it's a pure, deterministic
  derivation of the owner's `CurrentSettlement.Village.VillageType.PrimaryProduction`, the same derivation
  vanilla's own `OnGameLoad()` re-runs on every load (not even a `[SaveableField]`).
- `RuralNotableInnAndOutIssue`'s `_targetSettlement`/`_boardGameType` fields are derived the same
  safely-recomputable way (confirmed by its own `OnGameLoad()` override).
- `HeadmanNeedsGrainIssue`'s ctor is genuinely empty — `NeededGrainAmount`/`AlternativeSolutionNeededGold` are
  pure getters computed on demand from `IssueDifficultyMultiplier` (deterministic, never an ambient roll) and
  the behavior-singleton `_averageGrainPriceInCalradia` (kept byte-identical across peers by
  `HeadmanNeedsGrainPriceCachePatches`, a separate mechanism since it's owned by the behavior, not the Issue).
- `LandLordNeedsManualLaborersIssue`/`BettingFraudIssue`/`GangLeaderNeedsSpecialWeaponsIssue` all have a plain
  `(Hero)` ctor that rolls nothing — their real per-client-divergent rolls happen later, at accept time inside
  `GenerateIssueQuest` (see each type's own bespoke interface for the accept-time capture that matters).

## NewIssueTypesAlternativeSolutionPatches — HourlyTick registration pattern

Every `RegisterEvents` postfix in this file does the same thing: adds `OnHourlyTick` as a
`CampaignEvents.HourlyTickEvent` listener for one vanilla behavior type, so `AlternativeSolutionCompletionRunner`
gets a chance to fire for that type's client-owned alternative-solution completions. No per-type logic beyond
the registration itself. Cross-assembly SandBox.dll types (RuralNotableInnAndOut, ProdigalSon, TheSpyParty,
RivalGangMovingIn, SnareTheWealthy) patch identically to TaleWorlds.CampaignSystem types via the same
compile-time `[HarmonyPatch(typeof(...))]` attribute, since `GameInterface.csproj` references `SandBox.dll`
directly.

Real incident (Deliver the Herd): its `AlternativeSolutionMirrorEligible` registration and this file's matching
`HourlyTick` registration were split across separate commits, and the second was initially missed — since
`IssueManager.DailyTick` (the only other path to `CompleteIssueWithAlternativeSolution`) is server-only, a
client-owned alternative-solution completion could never fire, leaving the quest permanently stuck in
`SolvingWithAlternativeSolution`. Both registrations are added in the same commit from then on specifically to
avoid repeating this.

`AlternativeSolutionScaleFlags` per type (confirmed against the decompiled source — `Casualties`/`FailureRisk`
types can genuinely fail but remain safe to route through the generic trigger since it's ownership-self-limiting,
not success-guaranteeing, not that the type always succeeds):
- Duration only (always succeeds deterministically): `HeadmanNeedsGrainIssue`, `HeadmanNeedsToDeliverAHerdIssue`,
  `ArtisanOverpricedGoodsIssue`, `GangLeaderNeedsWeaponsIssue`.
- None (Artisan does not override it; neither does GangLeaderNeedsToOffloadStolenGoods): `ArtisanCantSellProductsAtAFairPriceIssue`.
- `Casualties | FailureRisk` (genuinely can fail): `SmugglersIssue`, `CaravanAmbushIssue`,
  `MerchantArmyOfPoachersIssue`, `EscortMerchantCaravanIssue`, `SandBox.Issues.RivalGangMovingInIssue`,
  `SandBox.Issues.SnareTheWealthyIssue`.

## AwaitingAlternativeSolutionTroopsSaveableTypeDefiner — SaveBaseId

Base id `44_187_000` must stay unique among this project's `SaveableTypeDefiner`s.

## AlternativeSolutionCompletionAuthorityGuard / Runner

`AlternativeSolutionCompletionAuthorityGuard` lets the server's own authoritative
`CompleteIssueWithAlternativeSolution` call bypass the ownership-gate Prefixes
(`GenericQuestTypeAlternativeSolutionOwnershipGatePatch`/`NewIssueTypesAlternativeSolutionOwnershipGatePatch`):
`IsLocalPeerOwner` compares against `ControllerIdProvider`'s own local platform id, which a dedicated server's
own id can never equal a connected client's, so without this the server could never run the real completion for
a client-owned quest at all.

`AlternativeSolutionCompletionRunner.TryTriggerOwnedCompletion` is called from whichever peer's own HourlyTick
determines it is the recorded owner. On a client this only sends a request rather than completing directly:
`CompleteIssueWithAlternativeSolution` rolls an unseeded `MBRandom` check and grants every
reward/relationship/troop-XP/casualty consequence inline, so running it client-side would make that peer's own
local RNG state and campaign-state writes the sole, unmirrored source of truth for the outcome instead of the
server. `CompleteOnServer` is the server-side counterpart of the request branch — called either directly (the
owner IS the server, e.g. a listen-server host) or from a validated per-type request handler.

## IssueManagerAlternativeSolutionTroopsPatches

Replaces `IssueManager.TryToMakeTroopsReturn`/`CheckIfTroopsCanReturnToMainParty` entirely — vanilla's own
`_awaitingAlternativeSolutionTroops` is a single flat, non-per-owner `TroopRoster` field, which permanently
strands a disconnected owner's troops (nothing routes them back on reconnect) and can duplicate them across
every connected client if that field is ever non-empty. Fixed via `AwaitingAlternativeSolutionTroopsRegistry`,
keyed by the owning peer's `ControllerId` (resolved via `IssueOwnershipRegistry`) instead of `Hero` — by the
time troops reach this point, `IssueFinalized()` has already cleared the issue's own state, so the connection
identity is the only durable key left. Persisted alongside `IssueOwnershipRegistry`'s own save record.

Also fixes a separate dedicated-host NRE reachable through the same entry point: vanilla's
`DefaultIssueModel.CanTroopsReturnFromAlternativeSolution` dereferences `Hero.MainHero.IsPrisoner` with no null
guard, and `Hero.MainHero` is null on a dedicated server. `TryToMakeTroopsReturnPrefix` guards via
`IsLocalMainHeroSafelyAvailable` (checks `Game.Current?.PlayerTroop` rather than bare `Hero.MainHero`, since
evaluating `Hero.MainHero` at all throws the instant `Game.Current.PlayerTroop` is null) before ever calling the
model gate. `_inquiryInFlight` prevents a re-entrant `HourlyTick` (the inquiry callback is async) from stacking
a second inquiry. `BuildReturnedTroopsInquiryText`/`MakeAlternativeTroopsReturn` reimplement vanilla's own
private equivalents, the former with a null-companion-Hero guard vanilla itself lacks.

## Snapshot-before-iterate pattern (OnHourlyTick / ModuleRescanCompletionRunner)

Both `NewIssueTypesAlternativeSolutionPatches.OnHourlyTick` and `ModuleRescanCompletionRunner.Run` snapshot
`Campaign.Current.IssueManager.Issues` into a `List` before iterating: a genuine completion inside the loop
mutates `IssueManager.Issues` (removes the finalized entry), and `MBReadOnlyDictionary`'s enumerator doesn't
tolerate that.

## GenericQuestTypeDispatchPatches — priority and replay-skip

`GenericQuestTypeCreationTriggerPatch.Postfix` uses `[HarmonyPriority(Priority.First)]` because this dispatch
must publish before another postfix on the same method recurses through `network.SendAll` into another peer's
own `MessageBroker` context. The quest-solution-accept and alternative-accept trigger patches skip
re-publishing when `CallOriginalPolicy.IsOriginalAllowed()` (a mirror replay runs under `AllowedThread`) or
`IssueDispatchReplayGuard.IsActive` (a genuine server-side replay) — otherwise a replay would loop back through
the network again. `IssueDispatchReplayGuard` itself is a narrower alternative to `AllowedThread` for these
internal replay calls specifically: `AllowedThread` also tells every OTHER unrelated patch to skip its own
network-publish logic, which is wrong for a genuine server-authoritative replay that should still
register/replicate normally — this flag only tells `GenericQuestTypeDispatchPatches`' own postfixes to skip
re-triggering themselves.

## IsExternalInitPolyfill

Polyfill so C# 9 `record`/`init` compiles against netstandard2.0, which has no `IsExternalInit` type.

## IssueGiveCatalog / IssuesDebugCommand — debug tooling notes

`IssueGiveCatalog` has one entry per real vanilla `IssueBase` subtype (43 total). A "not wired" entry has a
null `Resolve` and a non-null `NotWiredReason`, which `IssuesDebugCommand` reports verbatim rather than
silently no-op'ing — debug `give` bypasses each type's own eligibility selection with simple always-safe
defaults. `NotableWantsDaughterFoundIssue`'s hero-minting side effect lives in its Quest ctor, not the Issue
ctor, so wiring its Issue-only `give` entry carries no special risk from this debug command.

`IssuesDebugCommand.Give`: vanilla only ever allows one active issue per hero at a time (`IssueManager.Issues`
is keyed by `Hero`) — rejects rather than silently overwriting an existing one. If `StartIssueQuest` throws
after `CreateNewIssue` succeeded, the hero is left with `Issue` stuck attached and no live quest; `Give` rolls
it back via `DeactivateIssue` so the hero is left exactly as it was found and immediately retry-able.

## IssuesDebugCommandTests (E2E) — fixture notes

- `Give_BettingFraud_...`: `BettingFraudIssue`'s own ctor takes no related object, but the real
  `StartIssueQuest` → quest-generation path this command's `give` drives unconditionally reads
  `Hero.CurrentSettlement` deep in vanilla/generic-dispatch code regardless of quest type — a hero with no
  current settlement at all throws well before reaching the quest type's own logic. Every bare-Hero type
  therefore still needs a minimal "currently standing somewhere" fixture, same as every related-object type.
- `Give_VillageNeedsTools_...`: same fixture shape as `VillageNeedsToolsIssueTests.SetupVillageOwner` — Hearth
  pinned to 650 (≥ the real ctor's 300 threshold) so the constructor always takes the gold-payment branch.
  Setup and the give/complete calls all run inside ONE `EnvironmentInstance.Call` — splitting them across two
  separate `Call` invocations re-enters `StaticScope` a second time, which was observed to reintroduce the same
  no-current-settlement harness/vanilla quirk described above.
- `Give_NotableWantsDaughterFound_ToHeroWithNoCurrentSettlement_...`: reproduces a real bug found by independent
  review. `NotableWantsDaughterFoundIssue`'s own ctor is a bare field-store (safe), but its quest — only ever
  constructed via `StartIssueQuest` → `IssueBase.StartIssueWithQuest` → `GenerateIssueQuest`, never via the
  Issue's own ctor — dereferences `Hero.CurrentSettlement` (`questGiver.CurrentSettlement.Village...`)
  unconditionally in its constructor. A hero with no current settlement therefore makes `CreateNewIssue`
  succeed and then `StartIssueQuest` throw; before the fix this left `hero.Issue` permanently non-null with a
  null `IssueQuest`, and both further `give`/`complete` calls on that hero refused forever. After the fix,
  `Give`'s `DeactivateIssue` rollback (see above) leaves the hero exactly as it was found and immediately
  retry-able with any quest type, which this test proves by retrying with `BettingFraud` right after.

Two things this bypass has to do manually, that a real creation `Handler.cs` would otherwise handle:
- `IssueManagerCreateNewIssuePatches`'s Prefix blocks `IssueManager.CreateNewIssue` unless
  `ModInformation.IsServer` or the calling thread is `AllowedThread` — the client-side construction must be
  wrapped in `using (new AllowedThread())` or it silently no-ops and returns `false`.
- `IssueGenerationRegistry` must hold the *same* generation value on both peers before any accept flow runs,
  or `GenericAcceptMirrorHandler`'s stale-generation check (task: re-validate accept requests with a
  per-Hero issue-generation stamp) rejects it. A real Handler syncs this via the creation broadcast; here
  the server's `Bump(owner)` return value is captured and applied via the client's own `SetGeneration(owner,
  generation)` call instead.

## QuestTypeDescriptor byte-blob accept fields

`TryArbitrateQuestSolutionAcceptBytes`, `MirrorQuestSolutionAcceptBytes`, and `MirrorAlternativeAcceptBytes`
are all `null` unless the quest type's registration called `WithQuestSolutionAccept`/`WithAlternativeAccept`
respectively — callers must null-check before invoking.

## NetworkVillageIssueCreated.ExchangeItemId

`ExchangeItemId` is `null` when the village pays in gold rather than goods; check for null before treating
it as an item reference.

## DisableAllIssueBehaviorsExceptAllowlist — dynamic patching and the orphaned-disable-patch bug

Allowlist targets aren't known at compile time (discovered by scanning `TaleWorlds.CampaignSystem`/`SandBox`
for `CampaignBehaviorBase` subclasses with a nested `IssueBase` type), so `RegisterEvents` is patched via a
runtime `Harmony.Patch(...)` call (`DynamicHarmony`) rather than a `[HarmonyPatch(typeof(...))]` attribute,
which requires the target type at compile time.

`VerifyAllowlistIntegrity` exists because of a bug that recurred three times (commits e96018702, 479f810e7,
and a 12-type sweep): a type added to `Allowlist` can still be silently blocked by a leftover standalone
`Disable<Type>IssueBehavior` Harmony patch elsewhere in the codebase, predating this allowlist — this
class's own scan only patches over non-allowlisted types, so it never notices or removes such a leftover
patch. `VerifyAllowlistIntegrity` asks Harmony directly whether any allowlisted type's `RegisterEvents`
still carries a prefix and logs an error if so. The fix for a failure here is always to delete the offending
orphaned patch class — never to add a gate/exception to it.

## Test setup: village.Hearth / hero.Occupation magic values

E2E fixtures across `VillageNeedsToolsIssueTests`/`AwaitingAlternativeSolutionTroopsTests` set two values that
look arbitrary but aren't: `village.Hearth = 650f` is above the vanilla constructor's 300 threshold, so
`VillageNeedsToolsIssue` always takes the gold-payment branch instead of the goods-exchange branch.
`hero.Occupation = Occupation.RuralNotable` is required because `IssueBase.IssueSettlement` (read by
`RewardGold`/`AlternativeSolutionStartLog`) returns `null` unless the owner `IsNotable` — omitting it NREs
any test that drives a real `StartIssueWithAlternativeSolution()` call.

## VillageNeedsToolsAlternativeSolutionCompletionPatches.OnHourlyTick — test-only reflection invocation

E2E tests invoke `OnHourlyTick` directly via reflection because the test harness never runs a live
`VillageNeedsToolsIssueBehavior`, so its real `RegisterEvents`/`HourlyTickEvent` wiring never fires in-test.

## Server-side return-time re-check in alt-completion tests

Since the server independently validates its own synced copy of `AlternativeSolutionReturnTimeForTroops`
before honoring a completion request (no longer a vacuous guard — see "AlternativeSolutionReturnTimeForTroops"
above), tests simulating "time has passed" must force `default` on both the client's and the server's own
copy of the field, not just the client's.

## AwaitingAlternativeSolutionTroopsTests — coverage and fixture notes

Covers the shared, cross-issue-type `AwaitingAlternativeSolutionTroopsRegistry` fix that replaces vanilla's
single flat, non-per-owner `IssueManager._awaitingAlternativeSolutionTroops`. Uses
`VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue` as the vehicle issue since it's already the
lightweight, established type this registry's own persistence piggybacks on.

- `AwaitingAlternativeSolutionTroopsRegistry` (like every other bare `internal static class ...Registry` in
  this project — `IssueOwnershipRegistry`, `LordsNeedsTutorQuestFlags`, etc.) is a single process-wide static,
  not reset between tests or scoped per `EnvironmentInstance`. Tests use a fresh `Guid`-suffixed controller ID
  per run rather than a shared literal to avoid cross-test collisions.
- `ClientOwnedAlternativeSolutionCompletion_WhileOwnerUnreachable_TroopsSurviveASaveReloadAndReturnOnReconnect`
  drives the real production trigger (`VillageNeedsToolsQuestType.TryTriggerOwnedAlternativeSolutionCompletion`,
  an `IsLocalPeerOwner`-gated per-issue `HourlyTickEvent` listener that runs on the owner's own machine)
  through: (1) a genuine client accept, (2) the owner's own client hitting the real
  `IssueManager.TryToMakeTroopsReturn` entry point while `MobileParty.MainParty` is absent (simulating
  mid-battle/captured, one of the model gate's real failure conditions) so troops land in the per-owner
  registry instead of vanishing, (3) a real `IssuesCampaignBehavior.SyncData` save+reload proving server-side
  persistence, (4) a simulated reconnect where the real, patched `CheckIfTroopsCanReturnToMainParty` fires and
  a captured `InformationManager.OnShowInquiry`/`InquiryData.AffirmativeAction` is invoked, exactly matching a
  player clicking "OK".
  - The man-count assertions only check "at least 1", not an exact count: the Prefix's own local deposit and
    the mock network's loopback delivery of the forwarded `RequestAwaitingAlternativeSolutionTroopsDeposit`
    both eventually write into the one shared-process registry, and delivery isn't guaranteed to have landed
    by every checkpoint (GameThread-queued). The meaningful invariants are the final ones — the companion
    becomes `Active` and rejoins a real `MobileParty`'s roster.
  - `Hero.MainHero.HeroState` is a bare, non-isolated engine static not scoped per `EnvironmentInstance.Call`
    the way `Campaign.Current`/`Game.Current` are, and can drift to a stale value from an earlier `Call()`
    boundary — re-normalized immediately before the drain assertion rather than trusting an earlier
    restoration to still hold.
- `TryToMakeTroopsReturn_HeadlessServer_NoCrash_TroopsDeposited` covers vanilla's
  `DefaultIssueModel.CanTroopsReturnFromAlternativeSolution` dereferencing `Hero.MainHero.IsPrisoner` with no
  null guard, called unconditionally by `IssueManager.TryToMakeTroopsReturn` even for a headless server.
  `IssueManagerAlternativeSolutionTroopsPatches.TryToMakeTroopsReturnPrefix`'s early-return already avoids
  this for the common case (empty roster); this test proves `IsLocalMainHeroSafelyAvailable` is still correct
  for a non-empty roster reaching the Prefix on a headless machine, a case no current call path produces but
  the guard must not assume away. Simulated via `Game.Current.PlayerTroop = null` (matching
  `LordWantsRivalCapturedIssueTests`'s established pattern), not a bare `Hero` field.
- `InquiryCaptureHandler` bridges to `TaleWorlds.Library.InformationManager.OnShowInquiry`/
  `InquiryData.AffirmativeAction` purely via reflection because this test project's Publicized copy of
  `TaleWorlds.Library` and GameInterface's own non-Publicized reference to the same physical DLL both surface
  a same-named type the compiler can't disambiguate (CS0229). `MakeDelegate` binds `OnShowInquiryEvent`'s
  `Action<InquiryData, bool, bool>` handler type from an `Action<object>` callback via standard delegate
  parameter contravariance, so `InquiryData` never needs to appear as a compile-time type.
