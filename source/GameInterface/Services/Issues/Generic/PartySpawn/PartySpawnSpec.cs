using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.PartySpawn;

/// <summary>
/// [Client/triggering peer, inside the real spawn method's own Harmony Prefix] Captures whatever per-accepter-
/// divergent scalar state the server needs to reproduce the spawn correctly, derived from THIS machine's own
/// ambient state (e.g. <see cref="MobileParty.MainParty"/>) - reading the SERVER's own ambient state instead
/// would be wrong whenever the genuine accepter is a remote client. Returns false (nothing to forward) when the
/// ambient state needed to compute it isn't available - the real Prefix still unconditionally skips the broken
/// local body either way; this only decides whether a request is actually published.
/// </summary>
public delegate bool TryCaptureForForwarding<TQuest, TCapture>(TQuest quest, out TCapture captured);

/// <summary>
/// Declarative record of which <see cref="PartySpawnTrigger"/> shape a given migrated type's spawn gate uses,
/// plus the per-instance reconciliation hooks that a trigger shape alone doesn't capture (a captured/forwarded
/// scalar; a bespoke deferred continuation; or neither, for the simplest instances).
///
/// <typeparamref name="TSpawned"/> - THE OUTPUT-SIDE GENERALIZATION (added when migrating CaravanAmbush/
/// SnareTheWealthy onto this primitive - the same class of gap <typeparamref name="TCapture"/> already closed
/// on the input side, see commit cbb6cf6ea's own doc comment): the original shape hardcoded a single
/// <see cref="MobileParty"/> as both <c>CreateOnServer</c>'s return type and <c>ApplySpawnResult</c>'s
/// force-write payload. That's right for Smugglers/EscortMerchantCaravan/MerchantArmyOfPoachers (each spawns
/// exactly one party, force-written onto exactly one quest field), but CaravanAmbush's real, private
/// <c>OnQuestAccepted()</c> force-writes TWO distinct fields (<c>_caravanParty</c>/<c>_banditParty</c>) and
/// SnareTheWealthy's real, private <c>SpawnQuestParties()</c> does the same
/// (<c>_caravanParty</c>/<c>_gangParty</c>) in the same accept-time step. The 3 existing single-party consumers
/// instantiate <c>TSpawned = MobileParty</c> directly - a bare type-parameter widen, zero behavior change
/// (<c>TSpawned</c> unifies with exactly what <c>CreateOnServer</c>/<c>ApplySpawnResult</c> already
/// returned/accepted before this change); CaravanAmbush/SnareTheWealthy instantiate
/// <c>TSpawned = (MobileParty, MobileParty)</c>.
///
/// Parameters:
/// <list type="bullet">
/// <item><description><c>AlreadySpawnedCheck</c> - reads whether this quest's spawn target already exists
/// (idempotency guard against a resent request re-creating a second party) - e.g. Smugglers'
/// <c>_smugglerParty != null</c>.</description></item>
/// <item><description><c>TryCaptureForForwarding</c> - [client/triggering peer] see
/// <see cref="TryCaptureForForwarding{TQuest,TCapture}"/>'s own doc comment.</description></item>
/// <item><description><c>ForwardSpawnRequest</c> - [client/triggering peer] publishes the captured payload for
/// this project's own Handler to pick up and forward over the network (a client's own <c>SendAll</c> only ever
/// reaches its one connection).</description></item>
/// <item><description><c>CreateOnServer</c> - [server only] the real, parameterized reimplementation of the
/// vanilla spawn method's body, substituting the captured payload for whatever the real body's own
/// (server-side-wrong-when-a-remote-client-accepted) ambient reads would have produced. Also expected to
/// force-write the result onto the creating peer's OWN quest instance before returning it (the genuine
/// server-side creation is never itself mirrored back through <c>ApplySpawnResult</c>).</description></item>
/// <item><description><c>ApplySpawnResult</c> - force-writes genuinely-created party/parties (received via
/// network broadcast) onto THIS peer's own quest instance.</description></item>
/// </list>
/// </summary>
public sealed record PartySpawnSpec<TQuest, TCapture, TSpawned>(
    PartySpawnTrigger Trigger,
    Func<TQuest, bool> AlreadySpawnedCheck,
    TryCaptureForForwarding<TQuest, TCapture> TryCaptureForForwarding,
    Action<Hero, TCapture> ForwardSpawnRequest,
    Func<Hero, TCapture, TSpawned> CreateOnServer,
    Action<TQuest, TSpawned> ApplySpawnResult
) where TQuest : QuestBase;
