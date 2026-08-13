# Issues subsystem — design notes

Non-obvious constraints and prior-incident context for the Issues quest-sync subsystem, kept out of
inline comments per the zero-comments code policy.

## Smugglers — party-spawn capture, creation ordering, and the two Harmony gates

`SmugglersQuestType.TryCaptureAccepterPartyStats` captures `desiredMenCount`/`customPartyBaseSpeed` from
`MobileParty.MainParty` at the moment the accepter's own client calls `CreateSmugglerParty()`, and
`CreateReplicatedSmugglerParty` uses only that captured tuple, never re-reading `MobileParty.MainParty` on
the server — on the server that property would resolve to the wrong party (the server's own, not the
accepter's) whenever a remote client is the one accepting.

`CreateReplicatedSmugglerParty`'s `CustomPartyComponent.CreateCustomPartyWithTroopRoster` call is
deliberately NOT wrapped in `AllowedThread`: it must look like a genuine, novel server-side creation so
`CustomPartyComponentLifetimePatches`/the `MobileParty` auto-registry take their real "assign an id and
broadcast it" branch instead of the "already-synced replay" branch.

`SmugglersPartySpawnGatePatch` exists because `CreateSmugglerParty()`'s `CustomPartyComponent` construction
is hard-blocked on a client (`CustomPartyComponentLifetimePatches.Prefix`), which would otherwise leave a
broken, split-brain "ghost" party only the client can see — unmodified on the server, forwards the request
on a client instead of running the local (broken) construction.

`SmugglersQuestOwnershipGatePatch` gates `SucceedQuest`: the smuggler-party dialogue (bribe/persuasion) is
registered on every peer, and `_smugglerParty` is force-mirrored to every peer once spawned, so without this
gate any connected player who talks to the shared party leader could complete and collect someone else's
already-accepted quest.

## GangLeaderNeedsToOffloadStolenGoods — CounterOfferHero ordering, CounterOfferGold, IsSettlementBusy

`CounterOfferHero` is deliberately NOT force-written in `CreationCaptureStrategyImpl.ConstructReplicated`:
`IssueManager.CreateNewIssue` calls `AfterIssueCreation()` *after* the factory returns, which independently
re-derives it — forcing it beforehand would be silently overwritten. It's force-written instead via
`ConstructAndRegisterReplicated`'s `afterRegistered` hook, the opposite order from every other creation-
capture type in this project.

`QuestSolutionAcceptMirrorStrategy.MirrorQuestAccepted` force-writes `CounterOfferGold` even though the real
`Quest` ctor independently re-derives its own from a live `Town.GetItemPrice` read — without the force-write
that live re-read would silently diverge from the accepted terms every peer already agreed on.

`GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches` deliberately does NOT gate `IsSettlementBusy`
(Issue- or Quest-level): it's a read-only query that answers consistently off the already-synced hideout
reference for every peer, so there's nothing for ownership to protect there.

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

## GenericAcceptMirrorIssueTypes — deliberate exclusions

A vanilla Issue type is deliberately left out of `QuestSolutionMirrorEligible`/`AlternativeSolutionMirrorEligible`
when its `GenerateIssueQuest` rolls or re-derives something at accept time that this shared, no-extra-data
mirror can't carry:
- `TheSpyPartyIssue` (quest-solution accept) — rolls the spy identity itself at accept time.
- `GangLeaderNeedsToOffloadStolenGoodsIssue` (both) — re-derives price/reward from live state at quest-solution
  accept time, and has its own bespoke Generic-shape accept mirror for the alternative-solution path (captures
  failure chance/casualty count/companion reward skill/troop XP — more than this shared mirror forces — plus
  its own dedicated completion trigger/ownership gate instead of `NewIssueTypesAlternativeSolutionPatches`'
  shared `HourlyTick` scan).
- `SnareTheWealthyIssue` (quest-solution accept only) — rolls a genuine random target settlement; it IS in
  `AlternativeSolutionMirrorEligible`.

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

## HeadmanNeedsGrainPrice — weekly grain-price cache broadcast

`HeadmanNeedsGrainIssueBehavior._averageGrainPriceInCalradia` is a behavior-singleton cache (not per-issue
state) recomputed via `CacheGrainPrice()` on `WeeklyTick`/`OnGameLoadFinished`/`OnNewGameCreatedPartialFollowUp`
— all three funnel through that one private method. Deliberately not folded into `SimpleIssueFactoryRegistry`:
there's no Issue/Quest instance to attach this to at the moment it changes, since it updates on its own weekly
cadence independent of whether any hero has an active `HeadmanNeedsGrainIssue` at all.

`HeadmanNeedsGrainPriceCachePatches` blocks `CacheGrainPrice()` on a client entirely (never recomputes
locally, to avoid relying on every client's own local market data being instantaneously synced at the exact
moment its own ambient WeeklyTick fires) and captures only a genuine server-side recompute, publishing it
locally. `HeadmanNeedsGrainPriceHandler` broadcasts that to every connected client, which force-writes the
value directly into its own local singleton instance instead of recomputing. A newly (re)connecting client
instead gets the current authoritative value for free via `HeadmanNeedsGrainPricePersistencePatches` riding
the existing `TransferSaveState` full-save-transfer join flow (piggybacks on `SyncData`, empty in vanilla) —
the broadcast only needs to cover already-connected peers for the ongoing in-session weekly update, not join.

`HeadmanNeedsGrainPriceTests` fixture notes: `Helpers.QuestHelper.GetAveragePriceOfItemInTheWorld` (the real
method behind `CacheGrainPrice()`) divides by the count of towns/villages it finds in `Settlement.All` — a
world with zero registered settlements throws `DivideByZeroException`, so every peer needs at least one real
Town. `Settlement.All` is `Campaign.Settlements`, backed by `MBObjectManager` — the test's own
`SettlementBuilder` (`new Settlement(...)`) never registers there on its own, unlike this project's own
`IObjectManager` registration, so the fixture also calls `MBObjectManager.Instance.RegisterObject` directly.
`Town.GetItemPrice` also NREs unless `EnsureMarketData` (same pattern as `GangLeaderNeedsToOffloadStolenGoodsIssueTests`)
has given the Town a real `_marketData` first.

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

## HeadmanNeedsToDeliverAHerd — ownership gate, save-transfer join, and the herd-value item fixture

`HeadmanNeedsToDeliverAHerdOwnershipGatePatches` patches `DeliverHerdOnConsequence`/`DeliverHerdRejectOnConsequence`
directly because neither Consequence method has a gateable Condition of its own to hook instead.

`PostLoadReRegistration_StillGatesTheDeliveryDialogueForANonOwningPeer` covers the one remaining path that still
builds a fully-live, dialogue-registered mirror `Quest` on a non-owner: a peer that only ever received the real
`QuestBase` through the save-transfer-join path (the host owns the quest, a new client joins mid-session and its
save-transfer snapshot includes the host's own live quest object) re-registers the delivery `DialogFlow` via
`InitializeQuestOnGameLoad`/`InitializeQuestOnLoadWithQuestManager` entirely outside of
`QuestAcceptedConsequences`. The ownership gate is patched directly onto the Consequence methods themselves (not
the registration call sites) specifically so this out-of-band re-registration path is covered too. The test
builds `OtherClient`'s quest via the real, public `IssueBase.StartIssueWithQuest()` rather than a live accept
broadcast, since an ordinary accept-mirror no longer builds a real `Quest` object on any non-owner at all — this
is a faithful stand-in for what save-transfer deserialization itself hands a joining client.

In `SetupIssueOwner`, the owner's village must bind to a separate real `Town`, not itself — a self-referential
`Village.Bound` would make `Settlement.MapFaction` recurse into itself the moment
`DeliverHerdRejectOnConsequence` reads it. The herd item fixture needs an explicit `ItemCategory` assigned
(`herdTypeToDeliver.ItemCategory = herdCategory`) — a bare test `ItemObject` with no category NREs inside
`TownMarketData.AddNumberInStore`. `Campaign.Current.PlayerTraitDeveloper` must be force-initialized since the
test harness never sets it up and `OnIssueSolvedThroughQuest` reads it unconditionally.

## LandLordTheArtOfTheTrade — why only the turn-in conversation needs gating

Creation and accept are both fully generic on this tip already (`SimpleIssueFactoryRegistry` +
`GenericAcceptMirrorIssueTypes`'s two HashSets) — this quest only needed one bespoke piece.

`LandLordTheArtOfTheTradeIssueQuest`'s initial "how do you want to pay this back" offer (`OfferDialogFlow`) is
safe unguarded — it only ever runs inline in the same live conversation that just accepted the quest (a mirrored
replay never opens a local conversation), exactly like every other type's initial accept dialogue. The
exploitable turn-in instead lives entirely in the later, separate `DiscussDialogFlow` ("quest_discuss")
conversation, reachable by any peer who can walk up to the (shared, mirrored) quest giver — its own gating
condition (`QuestCanBeFinalized`) only checks `Hero.OneToOneConversationHero == QuestGiver` plus this machine's
own locally-tracked `_soldCount`/`_gatheredDenars`, with no ownership concept at all.
`LandLordTheArtOfTheTradeOwnershipGatePatches` gates the three real turn-in consequence methods directly (same
shape as the other quest-turn-in gate patches this session) rather than the dialogue option's own `Condition`,
since that's the simplest single choke point covering every player-option branch (paid-in-full success,
under-sold-but-pays success, and the "refuse to pay" failure).

`OnTimedOut` applies the same crime-rating/relation penalty as `QuestFailedPlayerBrokeTheAgreement` but is driven
by `QuestManager.HourlyTick -> CompleteQuestWithTimeOut()`, which isn't host/client-gated anywhere in this
codebase — every peer's own mirrored quest independently reaches its due time at the same in-game moment and
would otherwise apply the penalty once per connected peer instead of once, total.

No dedicated E2E tests were added for this type (matching the original, deliberate call on this quest before the
framework rebuild) — the ownership-gate pattern gated here is identical, field-for-field, to the pattern already
covered by dedicated tests on several other quest branches this session, and creation/accept are exercised by
the shared framework suite.

## ArtisanCantSellProductsAtAFairPrice — test fixtures, CounterOfferHero capture, IsThereLordSolution

`ResolvedMainHeroField` is set via reflection in tests because GameInterface's `ChangeRelationActionPatches`
routes `ApplyPlayerRelation` through an internal, non-visible `[ThreadStatic]` field instead of `Hero.MainHero` -
consequences that change relations NRE without it. `StubMapDistanceModel` stands in for the real
`DefaultMapDistanceModel`, which needs navmesh/pathing data this test harness never loads - it returns constant
distances so `SelectTargetSettlement`'s search can succeed instead of NREing. `SelectCounterOfferHero` requires
`Occupation.Merchant` specifically, not any notable, for the counter-offer hero fixture.

`PostLoadReRegistration_StillGatesBothDialoguesForANonOwningPeer` covers the same out-of-band path documented
under HeadmanNeedsToDeliverAHerd above: `InitializeQuestOnGameLoad` re-registers both dialogues outside
`QuestAcceptedConsequences`, so a pure mirror peer that never accepted (simulated via a direct
`StartIssueWithQuest()`, standing in for save-transfer deserialization) still needs the ownership gate to apply.

The creation-capture strategy captures `CounterOfferHero` via the shared `IssueBase.CounterOfferHero` property
(not a type-specific field) alongside the three type-specific fields - the same base-class property capture
pattern used by GangLeaderNeedsToOffloadStolenGoods (see that section above).

`ArtisanCantSellProductsAtAFairPriceLordSolutionDisablePatch` force-disables `IsThereLordSolution` to `false` -
vanilla's lord-solution path isn't part of this quest's synced flow and is deliberately kept unreachable rather
than built out.

## ArtisanOverpricedGoods — antagonist freeze, live-recompute divergence, test fixtures

`ArtisanOverpricedGoodsAntagonistFreeze` is keyed by `QuestGiver` (not `QuestGiver.Issue`/`IssueOwner`) so a
mid-quest `IssueManager.ChangeIssueOwner` transfer (e.g. the artisan notable dying) can't invalidate the freeze
lookup - that call only mutates `Hero.Issue`/`IssueBase.IssueOwner`, never this Quest's own already-snapshotted
`QuestGiver` field. `SaveBaseId 44_186_000` must stay globally unique across every `SaveableTypeDefiner` in the
project - collisions corrupt saves silently; other definers on this tip use 44_177_000/44_182_000/44_183_000/
44_187_000.

`AntagonistHeroPrefix`'s fallback-to-live-derivation path fails loudly (`Logger.Error`) rather than silently,
because `AntagonistByQuestGiver` is a plain in-memory static that's empty after every save/reload until
`ArtisanOverpricedGoodsAntagonistFreezePersistencePatches` restores it from `SyncData` - `InitializeQuestOnGameLoad`
rebuilds the Quest without re-running its ctor, so this is reachable in practice (a save created before this fix
shipped), not just hypothetically.

`ArtisanOverpricedGoodsIssueHandler` logs loudly (not just silently proceeds) if `RequestedTradeGoodAmount`/
`RewardGold` land on exactly 0 right after creation: `ArtisanOverpricedGoodsIssue.OnGameLoad` silently
re-derives whichever is 0 on every peer's own NEXT save/load using THAT peer's own live
`IssueDifficultyMultiplier` - a real cross-peer divergence risk if it were ever allowed to happen quietly.

`ArtisanOverpricedGoodsCompleteQuestGatePatch` exists because the full-delivery dialogue chains
`base.CompleteQuestWithSuccess` as a SEPARATE `.Consequence` node after `DeliverItemsFullyOnConsequence` in the
same dialogue option - gating only that method (a prefix skipping its own body) would not stop the dialogue
from still reaching completion next. Gated directly on the shared `QuestBase.CompleteQuestWithSuccess`, scoped
to this Quest type only - every other type passes through unmodified.

Test fixture notes: `ResolvedMainHeroField`/`EnsureMarketData`/`PlayerTraitDeveloper` follow the same pattern
documented under ArtisanCantSellProductsAtAFairPrice above. `SetCampaignSettlements` force-sets
`CampaignObjectManager.Settlements` because `ArtisanOverpricedGoodsIssue`'s ctor unconditionally calls
`CalculateTradeGoodsAmountAndReward() -> QuestHelper.GetAveragePriceOfItemInTheWorld`, which iterates
`Campaign.Current.Settlements` (a cached snapshot never auto-populated by this harness) and divides by the
count found. `hero.Clan.SetLeader(hero)` (and for the counter-offer/decoy heroes) is needed because
`DefaultDiplomacyModel.GetHeroesForEffectiveRelation` redirects a relation change to `hero.Clan.Leader`
whenever `Clan != null`, and `HeroBuilder` never assigns one by default. The test's highest-value case
(`AntagonistHero_UsesTheFrozenCounterOfferHero...`) deliberately inserts a DECOY merchant notable into
`Settlement.HeroesWithoutParty` BEFORE the real `CounterOfferHero` so `Settlement.Notables` (built off that same
insertion order) lists the decoy first - proving the harness genuinely reproduces the real vanilla-adjacent
`Notables.FirstOrDefault` bug this fix replaces, not just a theoretical one.

## ArtisanOverpricedGoods accept-request fixture: IssueStayAliveConditions needs a price factor > 1.8

`ArtisanOverpricedGoodsIssue.IssueStayAliveConditions()` requires
`IssueOwner.CurrentSettlement.Town.GetItemCategoryPriceIndex(_requestedTradeGood.ItemCategory) > 1.8f` (plus
`CounterOfferHero.IsActive` and a settlement match) - this is checked server-side by
`GenericAcceptMirrorHandler.Handle_RequestGenericIssueAcceptQuest`/`Handle_RequestGenericIssueAcceptAlternative`
(the rebuilt framework's accept re-validation, task "Re-validate accept authorization with issue-identity
stamp") but NOT by the direct-`StartIssueQuest`-call path other tests use via `AcceptOnInstance` - only the
network accept-*request* path re-derives this. The shared `StubTradeItemPriceFactorModel` (used by several
quest types' tests) returns a flat `1f` from `GetBasePriceFactor`, which resolves the price index to exactly
`1f` - below the 1.8 threshold - so any test that drives a real `RequestGenericIssueAcceptQuest`/
`RequestGenericIssueAcceptAlternative` for this quest type needs a price factor override above 1.8, not the
shared stub's default. `OverpricedTradeItemPriceFactorModel` (a local subclass overriding only
`GetBasePriceFactor` to `2f`) is used instead in this file's `SetupIssueOwner`, rather than editing the shared
stub, since other quest types' fixtures may rely on its flat 1f and this repo builds/tests each quest type on
its own independent branch.

## ArtisanOverpricedGoods accept-request fixture, continued: CounterOfferHero settlement + real party roster

Two more fixture gaps surfaced by the same server-side re-validation as above: `IssueStayAliveConditions()`'s
`CounterOfferHero.CurrentSettlement == IssueSettlement` check needs `CounterOfferHero.StayingInSettlement`
explicitly set (`Settlement.AddHeroWithoutParty` does not set it - `Hero.CurrentSettlement` falls back to
`StayingInSettlement` only when the hero has no party and isn't a prisoner). And
`GenericAcceptMirrorHandler.Handle_RequestGenericIssueAcceptAlternative`'s server-side troop-selection validation
(`ApplyValidatedSentTroops` -> `IPrisonerSaleValidator.Validate(claimedRoster, party.MemberRoster)`) intersects
the claimed troops against the requester's real registered `MobileParty` roster - a `Player` registered with no
real `MobilePartyId` (or an empty roster) validates down to nothing, leaving `AlternativeSolutionSentTroops`
empty server-side and NREing inside vanilla's `AlternativeSolutionStartLog` (`AlternativeSolutionHero` derives
from the first hero-type troop in that same roster). Tests exercising a real `RequestGenericIssueAcceptAlternative`
need a registered `MobileParty` with the companion in its `MemberRoster`, referenced via the `Player` ctor's
`mobilePartyId` parameter - same pattern `AwaitingAlternativeSolutionTroopsTests` already established.

## ArtisanOverpricedGoods accept-request fixture, correction: alternative-solution companion must NOT be CounterOfferHero

The real root cause of the `AlternativeSolutionAccept` test's `IssueStayAliveConditions()` failure (price factor
and settlement were both fine) was reusing `CounterOfferHero` as the alternative-solution "companion sent" hero.
Real `StartIssueWithAlternativeSolution()` calls `DisableHeroAction.Apply(AlternativeSolutionHero)` on whichever
hero was added to `AlternativeSolutionSentTroops` - this disables that hero (`HeroState -> Disabled`), and this
project's AutoSync mirrors the state change to every peer, including the server, before/as the accept-request is
validated. Since `IssueStayAliveConditions()` requires `CounterOfferHero.IsActive`, reusing the same hero for
both roles makes the accept-request fail server-side validation (a *silent* rejection - the final
`GenericAcceptMirrorHandler` gate branch sends `NetworkGenericIssueAcceptRejected` with no `Logger.Error`, unlike
the unregistered-requester and stale-generation branches above it). Fixed by using a dedicated, separate
companion hero for the alternative-solution roster instead of reusing `CounterOfferHero`.

## CaravanAmbush — party-spawn capture, the whole-method gate, and two vanilla-adjacent bugs

Needs the `Generic/PartySpawn/` subsystem (`PartySpawnSpec.cs`/`PartySpawnTrigger.cs`/`PartySpawnRunner.cs`, same
as Smugglers - restored again on this branch) plus the `GameInterfaceModule.cs` DI registration
(`builder.RegisterType<PartySpawnRunner>().As<IPartySpawnRunner>().InstancePerDependency();`).

`OnQuestAccepted()` inlines both `CreateCaravanParty` and `CreateBanditParty` construction, both hard-blocked on
a client - unlike Smugglers there's no separable `CreateXParty()` to gate, so `CaravanAmbushPartySpawnGatePatch`
gates the whole method: the safe local side effects (`RunLocalAcceptSideEffects`) run directly, then party
creation/rewards are forwarded to the server instead. `CreateReplicatedAccept` uses the captured
`accepterMainPartySpeed` parameter instead of reading `MobileParty.MainParty.Speed` directly - on the server
that would resolve to the wrong party's speed whenever a remote client is the accepter. It also uses this
machine's own `MobileParty.MainParty` for the nearest-hideout lookup rather than the accepter's - a deliberate,
low-impact cosmetic simplification (only affects which nearby hideout the ambushers are attributed to) rather
than a correctness bug, since threading the accepter's own party through this lookup wasn't worth the
complexity. `_rewardItems` is a quest-level field, not part of any `MobileParty`/`TroopRoster`, so it isn't
covered by AutoRegistry sync and needs its own explicit force-write via `ForceAcceptedState` - `ApplySpawnResult`
only force-writes the two party fields; `ForceAcceptedState` applies the reward items separately afterward, so
`ApplySpawnResult` itself must never call `ForceAcceptedState` (would recurse).

`CaravanAmbushIssueHandler`'s creation and accept handlers are idempotent by construction (`if (owner.Issue !=
null) return;` on creation; `partySpawnRunner.AlreadySpawned(...)` on accept) - a resend must never create a
duplicate issue or spawn a second set of parties for the same quest.

`CaravanAmbushCaravaneerDialogNullGuardPatch` fixes a vanilla-adjacent bug: `GetCaravaneerDialogFlow()`'s "start"
Condition dereferences `_caravanParty` with no null check, unlike Smugglers' equivalent. `_caravanParty` is null
on any non-owner mirror (and briefly on the accepter itself) until the force-write lands, so any conversation on
that peer would NRE. The Condition is a compiler-generated method with no stable name, so it's located
reflectively rather than via `nameof`.

`CaravanAmbushQuestOwnershipGatePatch` gates two convergent completion paths - `OnQuestSucceeded()` (fight-win,
and the caravaneer's own gratitude dialogue) and `OnPlayerHiredBandits()` (the alternate "recruit the bandits
outright" success path) - since either is reachable as the Consequence of this quest's own dynamically-registered
dialogue.

Party-spawn tests deliberately do not drive `CreateReplicatedAccept` through vanilla's own bandit-hideout pick /
caravan-template roll (needs real map/faction/culture data out of scope for this harness); they use a real,
AutoRegistry-synced party built the same already-proven-safe way `SmugglersIssueTests` does instead.

## CaravanAmbush accept-request fixture: IssueStayAliveConditions needs a nonzero OwnedCaravans count

`CaravanAmbushIssue.IssueStayAliveConditions()` requires `IssueOwner.OwnedCaravans.Count > 0` (plus no war with
the player's clan) - checked server-side by the same accept re-validation as ArtisanOverpricedGoods above.
Building a real `CaravanPartyComponent` via `CaravanPartyComponent.CreateCaravanParty` NREs on this harness's
bare template/settlement data (same class of problem the vanilla party-spawn itself needs real map/culture data
for - see the party-spawn note above). Since the condition only checks `.Count`, not the entries themselves,
`owner.OwnedCaravans.Add(null)` satisfies it without needing any real party construction.

## EscortMerchantCaravan — no captured payload, bare reflective SpawnCaravan invoke, game-load fallback

Needs the `Generic/PartySpawn/` subsystem (restored again on this branch, same as CaravanAmbush/Smugglers) plus
the `GameInterfaceModule.cs` DI registration, and also `Generic/AcceptMirror/Unit.cs` (a trivial marker struct
for "no captured/forwarded payload" `PartySpawnSpec` instantiations) - neither existed on the current tip before
this branch; both were apparently dropped along with the rest of the pre-rebuild `Generic/` scaffolding and need
restoring per-branch for whichever quest type is the first to need them again.

This is the first `PartySpawnSpec<Quest, TCapture, TSpawned>` instantiation with `TCapture = Unit` (no captured
payload at all) - `SpawnCaravan()` reads only `QuestGiver`-derived state, so there's nothing accepter-specific to
capture or forward. `SpawnCaravanOnServer` is a bare reflective invoke of the real, private `SpawnCaravan()`
(not a hand-reimplementation like CaravanAmbush's `CreateReplicatedAccept`) - deliberately NOT wrapped in
`AllowedThread`, so it looks like a genuine, novel server-side creation (same as the host's own real accept path
already does, unwrapped) and `CustomPartyComponentLifetimePatches`/the MobileParty AutoRegistry both take their
real "server created this for the first time" branch. This also re-triggers
`EscortMerchantCaravanPartySpawnGatePatch`'s own Postfix on `SpawnCaravan()`, which is the intended single
broadcast point for both a genuine accept and a forwarded request - no separate broadcast call is needed in
`Handle_RequestPartySpawn`.

`SpawnCaravan()` calls `CustomPartyComponent.CreateCustomPartyWithTroopRoster`, hard-blocked on a client -
unlike Caravan Ambush, only `SpawnCaravan()` itself is gated (`StartQuest()`/the journal log stay local, safe to
run unmodified everywhere).

`EscortMerchantCaravanOwnershipGatePatches` gates the quest's whole ambient-tick/event lifecycle, not just
turn-in, because `QuestManager.OnGameLoaded() -> InitializeQuestOnLoadWithQuestManager()` re-runs
`RegisterEvents()`/`SetDialogs()` for real after a reload/join, which can leave more than one peer's mirror with
live listeners - each would independently dereference `_questCaravanMobileParty` and independently perform the
same world-mutating actions.

`EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch` is a fallback for a join-mid-quest/reload where the
deserialized `_questCaravanMobileParty` didn't resolve: re-finds the real, already-AutoRegistry-synced party by
component/owner/home-settlement match. Owner-gated (pointless for a non-owner - every dangerous read is already
blocked by the ownership gate patches regardless). If resolution genuinely fails, it must skip the original
method (returns false) and cancel the quest right there - otherwise the original unconditionally calls
`SetDialogs()` with the field still null, and the owner-gated `HourlyTick()` crashes on it on the very next tick.

`EscortMerchantCaravanCaravanTalkConditionNullGuardPatch` fixes a vanilla bug: `caravan_talk_on_condition()`
(shared by 3 of `SetDialogs()`'s 5 global dialogue flows) dereferences `_questCaravanMobileParty.MemberRoster`
with no null check. That field is only ever set by `SpawnCaravan()`, so it's permanently null on every
non-owner mirror - any conversation on that peer would NRE. The other 2 flows' condition delegates only do
reference comparisons against the field, so are safe and left untouched.

Test scope note (same established precedent as CaravanAmbush): `SpawnCaravan()`'s real body needs
`CaravanHelper.GetRandomCaravanTemplate` and a real "guard" `CharacterObject` lookup this bare harness doesn't
stand up, so the test file does not drive `SpawnCaravanOnServer`'s reflective real-body invocation to a clean
success - the gate decision itself is fully exercised, and request/broadcast/force-write convergence is
exercised using a real, AutoRegistry-synced `MobileParty` built via the same
`CustomPartyComponent.CreateCustomPartyWithTroopRoster` factory `SpawnCaravan()` itself calls. The game-load
fallback test drives a direct `StartQuest()`, not the full `QuestAcceptedConsequences()` - the real
`SpawnCaravan()` partially assigns `_questCaravanMobileParty` before throwing on this harness's missing
caravan-template database entry, which would falsely satisfy the fallback test without exercising it at all. The
client-owner spawn test tolerates an exception from `QuestAcceptedConsequences()` (the activation journal-log
text reads the static `Settlement.CurrentSettlement`, unset in this bare harness) since the party-spawn gate has
already run and forwarded its request by the time it throws.

## MerchantArmyOfPoachers — moving party spawn off a menu on_init callback, and the battle-start approval gate

Ported from the pre-rebuild roadmap branch (`origin/feature/village-needs-tools-sync`), rewritten onto the
current framework's `QuestTypeRegistry`/`PartySpawnSpec`/`IssueOwnershipRegistry` shape (the old branch predates
the whole framework rebuild and used a hand-rolled injectable-interface pattern).

The decisive bug this fixes: vanilla creates `_poachersParty` lazily, inside the `army_of_poachers_village`
game-menu's `on_init` callback (`if (Instance._poachersParty == null && !Hero.MainHero.IsWounded)
CreatePoachersParty();`) - whichever peer's own client first happens to open that menu (itself gated on purely
local night-time/`PlayerEncounter.IsPlayerWaiting` state, not any authoritative trigger) creates the
world-visible party. There is no gameplay reason to wait for a menu-open - decompiled source shows the party
just sits parked, AI-disabled, from the moment the quest starts. Fixed by moving the spawn call itself off the
menu callback entirely onto the accept-time flow: `MerchantArmyOfPoachersPartySpawnGatePatch` runs a **Postfix**
(not a Prefix-block, unlike every other party-spawn gate this session) on the real, unmodified
`QuestAcceptedConsequences()` - `StartQuest()`/`AddLog`/`AddTrackedObject` are all safe to run everywhere, so
nothing needs blocking; the postfix just triggers the poachers-party spawn afterward, gated the usual way
(block-and-forward-as-request on a client-owner, allow on server/host-owner). This is also why
`PartySpawnSpecInstance.Trigger` uses the `PartySpawnTrigger.DialogueConsequence` variant instead of
`SpawnMethodWrap` - descriptive only (the actual wiring is the bespoke Postfix, `PartySpawnSpec.Trigger` doesn't
programmatically drive anything). Vanilla's own `army_of_poachers_village_on_init` null-check is left completely
untouched - it becomes permanently-false dead code once the party always already exists by the time that menu
could ever open, which is harmless and not worth a separate patch to delete.

`CreatePoachersPartyOnServer` is NOT a bare reflective invoke of vanilla's own private `CreatePoachersParty()`
(unlike EscortMerchantCaravan's `SpawnCaravanOnServer`) - a real, load-bearing correctness issue rules that out:
vanilla's own body ends with `EnterSettlementAction.ApplyForParty(this._poachersParty, Settlement.CurrentSettlement)`,
which is only ever safe because vanilla's OWN trigger point only ever runs while the accepter is physically
standing inside `_questVillage.Settlement`. Since this fix deliberately triggers creation at a different,
earlier moment than vanilla intended, that invariant no longer holds - the reimplementation substitutes the
always-correct, always-non-null `_questVillage.Settlement` in its place, which is exactly what vanilla's own
call always evaluated to in every genuine playthrough - a faithful substitution, not a behavioral departure.
Everything else in the method body is a direct, unmodified port of vanilla's own logic. The bandit-clan/culture
pick (`SettlementHelper.FindNearestHideoutToMobileParty(MobileParty.MainParty, ...)`) is the one other
accepter-context read in the real body - accepted as a low-impact, cosmetic-only (troop-culture-skin) divergence
when this runs on the server on behalf of a remote-client owner, same precedent as CaravanAmbush's own
bandit-hideout pick.

`MerchantArmyOfPoachersBattleStartApprovalPatches` gates `StartQuestBattle()` - a named, directly
Harmony-patchable method (unlike Gang Leader Needs Weapons' inline lambda) reached from two real call sites, both
only reachable via the `army_of_poachers_village` menu (Category A - no non-owner peer can reach this today,
gated anyway as defense-in-depth). `StartQuestBattle()`'s own mission-launch calls
(`PlayerEncounter.RestartPlayerEncounter`/`StartBattle`/`CampaignMission.OpenBattleMission`) are local, UI/
mission-launch APIs that only ever make sense on the genuine owner's own machine - there is no "the server does
it instead" relocation available. Fix shape: ownership-gate outright for a non-owner; for the genuine owner, a
host runs vanilla unmodified, a remote client is blocked and forwards a request, and on server approval the
OWNER's own machine invokes the real `StartQuestBattle()` (wrapped in `AllowedThread` so this same Prefix steps
aside for that specific, already-approved call) while every other peer's mirror just flips
`_isReadyToBeFinalized` (parity-only bookkeeping, not load-bearing - that field is only ever meaningfully read
together with THIS machine's own local `PlayerEncounter.Current` inside `army_of_poachers_village_on_init`,
which never meaningfully runs for a non-owner anyway).

`MerchantArmyOfPoachersOwnershipGatePatches` closes an independently-found completion/turn-in gap:
`GetPoacherPartyDialogFlow()` is registered unconditionally from `SetDialogs()` (ctor AND
`InitializeQuestOnGameLoad()`) and its own top-level condition isn't owner-specific - now that the spawn-gate
fix correctly force-mirrors the real, globally-visible `_poachersParty` onto every peer, ANY connected player who
physically walks up to and talks to that shared party leader could reach this dialogue. Gates the two convergent
leaf methods reached via `ConversationEndOneShot` subscriptions inside the dialogue's own Consequence delegates -
`QuestSuccessPlayerComesToAnAgreementWithPoachers()` (persuasion-minigame win) and
`QuestFailedAfterTalkingWithPoachers()` (talk-down option) - without which any non-owner could steal or
unilaterally fail someone else's already-accepted quest. The "give up or die" option and the battle-outcome
paths are deliberately NOT gated - the former only sets a local, harmless flag already covered by the
battle-start gate above, and the latter are reached only from the same local-`PlayerEncounter` poll that has no
Category-B (dialogue) entry point for a non-owner, matching the reasoning already established for
`CaravanAmbushQuestOwnershipGatePatch`'s own non-gated failure paths.

`MerchantArmyOfPoachersInstanceResolutionPatch` is optional defensive hardening (not a cross-peer exploit - a
non-owner's own mirrored quest never calls `StartQuest()`, so it's never a candidate in vanilla's own "first
ongoing quest of this type" fallback scan). Resolves by matching `_questVillage`'s settlement against the local
player's current settlement/encounter context first, falling back to first-ongoing to preserve vanilla's
original permissiveness if nothing disambiguates - closes the case of the SAME local player running two
concurrent instances of this quest from two different merchant NPCs picking the wrong one.

Accept-request fixture note (same bug class as ArtisanOverpricedGoods/CaravanAmbush):
`IssueStayAliveConditions()` needs `IssueOwner.CurrentSettlement.Town.Security <= 90f` in addition to the quest
village not being under raid - the owner's OWN settlement (not just the quest village's) needs a real `Town`
component, or `.Town` NREs.

## RevenueFarming

Bespoke byte-blob accept strategy (`RevenueFarmingAcceptFields`/`RevenueVillageWireEntry`): unlike every other
migrated quest type so far, the captured accept fields include object references (the villages' `Settlement`s),
which `GenericAcceptFieldsSerializer`'s raw protobuf-net `Serializer.Serialize<T>` cannot serialize directly -
`TryCaptureQuestFields`/`MirrorQuestAccepted` resolve `IObjectManager` via `ContainerProvider.TryResolve` (same
service-locator pattern `AlternativeSolutionCompletionRunner`'s trigger sites already use) and do the
settlement-id pack/unpack themselves, since the fully-generic dispatch handler only ever sees opaque bytes.

Accept-request fixture note (same bug class as ArtisanOverpricedGoods/CaravanAmbush/MerchantArmyOfPoachers, 4th
confirmed instance): `ObjectHelper.SkipConstructor<Hero>()` does NOT leave `Hero.Clan` null - it comes back as a
real (if otherwise-empty) `Clan` instance with its own MBGUID. Vanilla's `IssueStayAliveConditions()` gates on
`_targetSettlement.OwnerClan == IssueOwner.Clan`; a bare test settlement's `OwnerClan` is genuinely null (no
Town/Village/Hideout component), so the comparison is `null == <non-null placeholder Clan>` and always fails
before it ever reaches the `BoundVillages` check. Fixed by force-writing `hero._clan = null` in the fixture
(bypassing `Hero.Clan`'s setter, which fires `OnLordRemoved`/`CampaignEventDispatcher` side effects unsafe to
run against a placeholder Clan) rather than giving the settlement a matching owner - `Town.OwnerClan`'s setter
cascades into `SetNewOwnerClan()`, which is worse to satisfy safely than just nulling the hero's side.

## MerchantNeedsHelpWithOutlaws

First quest type built genuinely from scratch this session (no prior branch to port from - confirmed via
`git ls-tree` across every branch, only orphaned `Disable*IssueBehavior` noise turned up). Its vanilla quest
tracks progress (`_destroyedPartyCount`/`_recruitedPartyCount`/`_validPartiesList`) continuously over the
quest's lifetime via nine separate `RegisterEvents` listeners (`HourlyTickParty`, `MobilePartyDestroyed`,
`OnBanditPartyRecruited`, `OnSettlementEntered`, `OnSettlementLeft`, `OnVillageRaided`, `OnMapEventStarted`,
`OnWarDeclared`, `OnClanChangedKingdom`), not just at accept time - `MerchantNeedsHelpWithOutlawsOwnershipGatePatches`
gates all nine to the owner only, same shape as every other quest's gate file. `IsSettlementBusy` (both the
Issue's and the Quest's own override) is deliberately left ungated - it's a pure read query with no state
mutation, and every peer (including a non-owner server) needs to answer it correctly so a second issue can't
get generated against the same hideout while this one is still active.

Alternative-solution wiring (`IsThereAlternativeSolution => true`) follows `VillageNeedsCraftingMaterials`'s
`IModuleRescanCompletionRunner` pattern exactly (`MerchantNeedsHelpWithOutlawsAlternativeSolutionCompletionPatches`)
rather than adding a new hardcoded entry to `NewIssueTypesAlternativeSolutionPatches.cs` - that file is Tier-1
legacy scaffolding for the fully-generic accept-mirror path, not something new bespoke (Path #2) quest types
should be added to.

`Handle_RequestQuestTypeAcceptAlternative` re-checks `IssueStayAliveConditions()` server-side, same as the
quest-solution accept path - a test exercising the alternative-solution accept race needs the same infested-hideout
fixture setup as the quest-solution accept tests, easy to miss since the alternative path doesn't go through
`Handle_RequestQuestTypeAcceptQuest` at all.

`Hideout.IsInfested` is a computed property (`Owner.Settlement.Parties.CountQ(IsBandit) >=
Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt`), not a settable
field - test fixtures need real `BanditPartyComponent.CreateBanditParty(...)`-constructed parties (pattern
copied from `HideoutMapEventTests`), not a direct property/field write.

Server-side troop validation on alternative-solution accept caps the mirrored `AlternativeSolutionSentTroops`
to the requester's own real registered party roster, same as the quest-solution accept path's village/settlement
capture - a test asserting the post-mirror troop count must assert the server-validated count (bounded by what
was actually added to the real party's `MemberRoster`), not the raw client-claimed amount.

## ProdigalSon

The blocker was never architectural - `Towns/Patches/Disabled/DisableProdigalSonIssueBehavior.cs` was an orphaned
pre-allowlist blanket disable (same shape as every other instance of this bug fixed throughout this session),
silently no-opping `RegisterEvents` so the issue could never be offered to any hero. Once deleted, the port
itself was a mechanical adaptation of an existing reference implementation (roadmap branch commit `11e06d067`)
onto the current `QuestType.cs`/`[QuestTypeModule]`/`CreationCaptureRunner` shape - that reference predates both
the generation registry (no `Bump`/`SetGeneration` calls at all) and the `IServerToClientCommand` message-direction
convention, both added here.

Rides the fully generic Path #1 accept-mirror (`GenericAcceptMirrorIssueTypes` already had both
`ProdigalSonIssue` HashSet entries pre-populated on the framework tip) - only creation needs bespoke capture,
for the two genuine creation-time RNG rolls vanilla performs before constructing the Issue:
`GetRandomElementWithPredicate` picks `_prodigalSon`, `SettlementHelper.FindRandomSettlement` (feeding
`_targetHero`) picks the target settlement/gang-leader hero. `_targetHouse` is then deterministically derived
inside the Issue's own constructor from `_targetHero.CurrentSettlement`, so it never needs independent capture.

Path #1's `GenericAcceptMirrorInterface.MirrorQuestAccepted` never constructs a real mirrored `IssueQuest` on a
non-owner peer - it only flips `_issueState` to `SolvingWithQuestSolution` (`IsSolvingWithQuest` on the Issue).
Same "only the accepter gets a real Quest object" shape already documented for MerchantArmyOfPoachers this
session; a naive test asserting every peer's `IssueQuest` is the real typed Quest will incorrectly fail on the
non-owner peer.

`ProdigalSonIssueQuest` has FOUR distinct player-interaction-driven completion paths, not just the usual
success/fail pair: a mission fight (`FinishQuestSuccess1`/`FinishQuestFail2`), a persuasion mini-game
(`FinishQuestSuccess3`), and paying the debt outright (`FinishQuestSuccess4`) - all reachable by any peer who
walks into the shared/mirrored reserved house and interacts with the shared/mirrored NPCs.
`ProdigalSonOwnershipGatePatches` gates all four; `FinishQuestFail1` (ambient timeout, symmetric across peers,
relation-only) is deliberately left ungated, matching precedent elsewhere.

Test-fixture note: the Issue's own constructor unconditionally calls `_targetHouse.ReserveLocation(...)`, which
NREs on a null `_targetHouse` unless `_targetHero.CurrentSettlement.LocationComplex` already contains at least
one `Location` with `CanBeReserved = true`. The plain `Location` constructor does NOT self-register into the
`LocationComplex` it's given (only the copy-constructor and `Initialize()` overloads do that, internally) - the
fixture has to add it to `locationComplex._locations` directly. Separately, `FinishQuestSuccess1`'s real body
touches `ChangeRelationAction.ApplyPlayerRelation` against `Hero.MainHero`, which NREs deep in
`CharacterRelationManager` if `Hero.MainHero` was never resolved - needs the same `Game.Current.PlayerTroop` +
`ResolvedMainHeroContext.ResolvedMainHero` fixture setup already established for RevenueFarming's completion test.
