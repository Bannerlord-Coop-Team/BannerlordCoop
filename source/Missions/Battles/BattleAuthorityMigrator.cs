using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Ownership changes when a player leaves a coop battle. A graceful leave or disconnect withdraws the player's
/// own-party troops on every client. When the departed player was the host, the server promotes a successor,
/// which adopts only the AI the old host was running (enemy side + allied NPC parties). Because that adoption
/// is local to the acting host, every other registry can keep inherited NPC agents keyed to an earlier host.
/// The promotion therefore also sweeps: the new host
/// adopts every agent still keyed to ANY controller no longer in the mission, not just the departed host's —
/// otherwise agents the old host merely HELD by adoption would be left driverless.
/// </summary>
public interface IBattleAuthorityMigrator : IDisposable
{
}

/// <inheritdoc cref="IBattleAuthorityMigrator"/>
public class BattleAuthorityMigrator : IBattleAuthorityMigrator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleAuthorityMigrator>();

    private readonly INetwork relayNetwork;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;
    private readonly IMissionContext missionContext;

    // Hosts whose own party withdrew — read when the promotion lands so the adoption knows to leave those
    // troops to the despawn instead of adopting them. Only touched from broker handlers,
    // which all run on the relay's receive thread, so no locking. Entries are consumed by the promotion and
    // cleared if the controller re-enters (a later drop of the same controller must not be treated as a retreat).
    private readonly HashSet<string> withdrawnHosts = new HashSet<string>();

    public BattleAuthorityMigrator(
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner,
        IMissionContext missionContext)
    {
        this.relayNetwork = relayNetwork;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;
        this.missionContext = missionContext;

        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    // A controller (re-)entered the instance: clear any stale withdrawal marker. Its party is supplied from
    // the server reserve and spawned fresh; withdrawn agents are never reclaimed.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        withdrawnHosts.Remove(payload.What.ControllerId);
    }

    // A graceful leave withdraws the player's party on every client.
    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        HandlePartyWithdrawal(payload.What.ControllerId, payload.What.InstanceId, "retreated");
    }

    // A disconnect uses the same visible withdrawal as a retreat. The server forgets that player's reserve,
    // so a later rejoin fields a fresh party instead of reclaiming frozen survivors.
    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        HandlePartyWithdrawal(payload.What.ControllerId, payload.What.InstanceId, "disconnected");
    }

    private void HandlePartyWithdrawal(string controllerId, string instanceId, string reason)
    {
        if (instanceId != null && instanceId != session.InstanceId) return;

        // A departed HOST's own party withdraws, while the promoted successor adopts only the NPC forces it
        // ran. Marking this before the migration message arrives keeps the despawn and adoption sets disjoint.
        if (session.IsHostController(controllerId))
        {
            withdrawnHosts.Add(controllerId);
            Logger.Information("[BattleSync] Host {Controller} {Reason} — despawning its own party; migration adopts the AI it ran", controllerId, reason);
            DespawnOwnPartyTroops(controllerId);
            return;
        }

        // A non-host owns its own party. Select by origin ownership because PVP puppets may be on the opposite
        // side from this client's PlayerTeam.
        DespawnControllerTroops(controllerId);
    }

    // [All clients] Withdraw a departed HOST's own-party troops: its hero and the troops of the party it
    // leads, identified by OWNERSHIP (the agent's origin party), NOT by battle side — a host also fields
    // allied NPC parties on the player side, and those must keep fighting under the promoted successor.
    // FadeOut (not Die/MakeDead) so it is a withdrawal, not a casualty — the player keeps these troops on the
    // map (the server forgot its reserve on departure, so a rejoin re-flattens and re-spawns them fresh).
    private void DespawnOwnPartyTroops(string controllerId)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            if (!TryGetPlayerParty(controllerId, out var playerParty, out var playerHero))
            {
                // Can't tell the departed player's own troops from the AI parties it fielded — despawning the wrong
                // set is worse than keeping them fighting, so leave everything to the adoption (pre-fix behavior).
                Logger.Warning("[BattleSync] Cannot resolve the withdrawn party of host {Controller}; its troops will be adopted instead of despawned", controllerId);
                return;
            }

            int despawned = 0;
            var despawnedRiders = new HashSet<Agent>();
            foreach (var info in registry.GetAgents(controllerId))
            {
                var agent = info.Agent;
                if (agent == null || agent.IsMount || !IsOwnPartyAgent(agent, playerParty, playerHero)) continue;

                if (agent.IsActive())
                    agent.FadeOut(false, true);
                registry.RemoveAgent(info.AgentId);
                casualties.Forget(info.AgentId);
                despawnedRiders.Add(agent);
                despawned++;
            }

            // Only the mounts of the riders that just withdrew: the host's AI cavalry horses stay registered
            // so the promoted successor adopts them (authority transfer) along with their riders.
            CleanUpDepartedMounts(controllerId, despawnedRiders, allMounts: false);

            if (despawned > 0)
                Logger.Information("[BattleSync] Despawned {Count} withdrawn own-party troop(s) of host {Controller}", despawned, controllerId);
        });
    }

    // [All clients] Withdraw a departed NON-host's troops, selected by OWNERSHIP exactly like the host path
    // above: its hero and the troops whose origin party is the player's party. Battle side is NOT identity —
    // in a PVP battle those puppets can sit on the OPPOSING team of a remaining client, so the old
    // local-PlayerTeam side filter skipped every one of them and they leaked as inert, effectively unkillable
    // puppets keyed to a controller that no longer answers (the live BR-051 leak). When the player's party
    // cannot be resolved, fall back to the agents ASSIGNED to it (registry OriginalOwner == that controller) —
    // adoption preserves OriginalOwner, so agents it merely HELD from an earlier hosting stint are excluded
    // (those belong to the absent-controller sweep, not the retreat despawn). Our own retreat tears the mission
    // down (skip self); other clients drop its puppets. FadeOut (not Die/MakeDead) so it is a withdrawal, not a
    // casualty — the player keeps these troops on the map.
    private void DespawnControllerTroops(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (session.IsOwn(controllerId)) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            var troops = registry.GetAgents(controllerId);

            if (troops.Count == 0) return;
            if (Mission.Current == null) return;

            if (!TryGetPlayerParty(controllerId, out var retreaterParty, out var retreaterHero))
                Logger.Warning("[BattleSync] Cannot resolve the withdrawn party of {Controller}; despawning the agents originally assigned to it instead", controllerId);

            int candidates = 0;
            int despawned = 0;
            var despawnedRiders = new HashSet<Agent>();
            foreach (var info in troops)
            {
                var agent = info.Agent;
                if (agent == null || agent.IsMount) continue;
                candidates++;

                // Ownership, not side: the retreater's own party (origin party + the hero belt-check) — or,
                // when its party did not resolve, whatever is still originally assigned to it. An agent it
                // merely holds by adoption (OriginalOwner = a third controller) is not its party and must not
                // withdraw with it — that one stays for the absent-controller sweep.
                bool isRetreatersOwn = retreaterParty != null
                    ? IsOwnPartyAgent(agent, retreaterParty, retreaterHero)
                    : info.OriginalOwner == controllerId;
                if (!isRetreatersOwn) continue;

                if (agent.IsActive())
                    agent.FadeOut(false, true);
                registry.RemoveAgent(info.AgentId);
                casualties.Forget(info.AgentId);
                despawnedRiders.Add(agent);
                despawned++;
            }

            // A non-host owns nothing but its own party, and nobody adopts a retreater — clear or re-key ALL
            // its registered mounts so none stays routed to a controller that no longer answers.
            CleanUpDepartedMounts(controllerId, despawnedRiders, allMounts: true);

            if (despawned > 0)
                Logger.Information("[BattleSync] Despawned {Count} withdrawn troop(s) of {Controller}", despawned, controllerId);
            else if (candidates > 0)
                Logger.Warning("[BattleSync] Withdrawal of {Controller} matched 0 of its {Count} registered troop(s) — selection found no own-party agents; anything left is keyed to a controller that no longer answers", controllerId, candidates);
        });
    }

    // [Game thread] Clean up a departed controller's registered MOUNTS. A horse registered to a controller
    // that no longer answers would be unkillable everywhere (hits on it route to its authority), so its
    // registry entries must not outlive it: the horses whose riders withdrew here (or that stand masterless)
    // fade out with the party; one that a LIVE rider of another owner took over transfers to that owner
    // instead — it stays registered (horse-keyed damage routing + death broadcast keep answering), and every
    // client resolves the same rider, so they all converge on the same new authority. With allMounts false,
    // only the withdrawn riders' horses are touched — the rest stay registered for the adoption path (a
    // promoted successor takes them over).
    private void CleanUpDepartedMounts(string controllerId, HashSet<Agent> despawnedRiders, bool allMounts)
    {
        var registry = coopMissionComponent.AgentRegistry;

        int removed = 0;
        int transferred = 0;
        foreach (var info in registry.GetAgents(controllerId))
        {
            var agent = info.Agent;
            if (agent == null || !agent.IsMount) continue;

            var rider = agent.RiderAgent;
            bool riderWithdrew = rider != null && despawnedRiders.Contains(rider);
            if (!allMounts && !riderWithdrew) continue;

            // A live rider of another owner is on this horse: re-key the horse to that owner rather than
            // dropping it to the unregistered fallback (which cannot broadcast the horse's death, and loses
            // movement authority entirely once that rider dismounts).
            bool ridden = rider != null && rider.IsActive() && !riderWithdrew;
            if (ridden
                && registry.TryGetAgentInfo(rider, out var riderInfo)
                && riderInfo.CurrentAuthority != controllerId
                && registry.TryTransferAuthority(riderInfo.CurrentAuthority, info.AgentId))
            {
                transferred++;
                continue;
            }

            if (!ridden && agent.IsActive())
                agent.FadeOut(false, true);

            registry.RemoveAgent(info.AgentId);
            removed++;
        }

        if (removed > 0 || transferred > 0)
            Logger.Information("[BattleSync] Cleaned up registered mount(s) of departed {Controller}: {Removed} removed, {Transferred} transferred to their riders' owners",
                controllerId, removed, transferred);
    }

    // [New host] The previous host departed and the server promoted us — adopt its orphaned agents so the
    // battle continues under us. Published only to the promoted client, so no host check here. Consumes the
    // withdrawal record: the old host's own party withdraws (DespawnOwnPartyTroops) and only the AI
    // it ran is adopted.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (payload.What.MapEventId != session.InstanceId) return;

        var previousHost = payload.What.PreviousHostControllerId;
        AdoptAgentsFrom(previousHost, "host migration", withdrawOwnParty: withdrawnHosts.Remove(previousHost));

        // The departed host may have inherited NPC agents through an earlier host migration. Other clients can
        // still key those agents to an older absent host, so adopting only the latest host would leave them
        // frozen. Sweep every absent authority in the migration chain. Idempotent: once swept, the agents are
        // keyed to us and a duplicate migration event finds nothing absent-keyed.
        SweepAgentsOfAbsentControllers(previousHost);

        // If the battle was already live when we were promoted, release the NPC AI we just adopted — a still-
        // deploying new host has AI ticking off, which would otherwise hold them frozen even though they were
        // moving under the previous host. If the battle was not live yet, this is a no-op (the gate holds).
        deployment.OnPromotedToHost();
    }

    // [New host] Adopt the agents of every controller that holds registry entries here but is no longer in
    // the mission — NPC orphans left behind by earlier adoptions in the host line that just departed. The
    // withdrawal record is consumed per swept controller for the same reason as the migration adoption above:
    // a departed host's own-party troops withdraw, and only the AI it ran should be (re-)adopted.
    private void SweepAgentsOfAbsentControllers(string previousHost)
    {
        var present = new HashSet<string>(missionContext.ControllersInMission);

        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            if (string.IsNullOrEmpty(controllerId)) continue;
            if (controllerId == previousHost) continue; // the migration adoption above already took these
            if (session.IsOwn(controllerId)) continue;
            if (present.Contains(controllerId)) continue; // still connected — its owner drives them

            AdoptAgentsFrom(controllerId, "host migration orphan sweep", withdrawOwnParty: withdrawnHosts.Remove(controllerId));
        }
    }

    // Take over the agents owned by the departed controller: move authority to us (so the movement poller
    // broadcasts them and the death/casualty path owns them — their attribution was captured at spawn) and
    // convert each inert puppet into a host AI combatant. Other peers keep them as puppets that follow our
    // movement. On departure the old host's own-party troops withdraw instead (despawned by
    // DespawnOwnPartyTroops on every client), so they are excluded here — the disjoint sets are what make the
    // despawn and this adoption race-free.
    private void AdoptAgentsFrom(string controllerId, string reason, bool withdrawOwnParty)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (session.IsOwn(controllerId)) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            PartyBase retreatedParty = null;
            Hero retreatedHero = null;
            if (withdrawOwnParty && !TryGetPlayerParty(controllerId, out retreatedParty, out retreatedHero))
                Logger.Warning("[BattleSync] Cannot resolve the withdrawn party of host {Controller}; adopting all of its agents", controllerId);

            var adopted = new List<CoopAgentInfo>();
            foreach (var info in registry.GetAgents(controllerId))
            {
                // A departed host's own-party troop withdraws, so it is not ours to adopt.
                // Neither is the horse under such a troop: it fades out with its rider (CleanUpDepartedMounts).
                if (retreatedParty != null && info.Agent != null && IsRetreatersAgent(info.Agent, retreatedParty, retreatedHero))
                    continue;
                adopted.Add(info);
            }

            if (adopted.Count > 0)
            {
                foreach (var info in adopted)
                    registry.TryTransferAuthority(session.OwnControllerId, info.AgentId);

                if (Mission.Current == null) return;

                // A migration can promote us while AI ticking is gated off (e.g. mid-deployment); turn it back on so
                // the adopted agents actually tick, exactly as the NPC-release path does.
                Mission.Current.AllowAiTicking = true;

                var interpolator = coopMissionComponent.AgentMovementHandler.Interpolator;
                var formations = new HashSet<Formation>();
                int aiCount = 0;
                foreach (var info in adopted)
                {
                    var agent = info.Agent;
                    if (agent == null || !agent.IsActive()) continue;

                    // The agent is no longer a puppet: stop reconciling it toward its former owner's last-reported
                    // position. The interpolator teleport-lerps every tracked agent back toward its target each
                    // frame, so a stale target overrides the AI and pins the agent in place — the "adopted troops
                    // don't move after host migration" freeze. A mounted puppet is tracked by its MOUNT's target,
                    // so forget that too.
                    interpolator.Forget(agent);
                    if (agent.MountAgent != null) interpolator.Forget(agent.MountAgent);

                    // An adopted MOUNT only changes authority (damage routing + death broadcast now answer to
                    // us) — it is not a combatant: no AI controller, no formation, no wake.
                    if (agent.IsMount) continue;

                    ConvertPuppetToHostAi(agent);
                    if (agent.Controller == AgentControllerType.AI) aiCount++;
                    if (agent.Formation != null) formations.Add(agent.Formation);
                }

                // The converted agents are AI-controlled now, but in a coop battle no general commands their
                // formation — and Formation.SetControlledByAI only issues a movement order when the formation AI
                // has an active behavior (there is none here), so they'd stand idle. Give each adopted formation
                // an explicit Charge so the NPCs actually engage. (Freshly spawned troops move because their
                // OWNER's team AI drives them; these adopted puppets have no such driver and need the order.)
                foreach (var formation in formations)
                    formation.SetMovementOrder(MovementOrder.MovementOrderCharge);

                // TEMP diagnostic: how many adopted agents actually became AI-controlled, across how many
                // formations (each ordered to Charge above). The per-formation state is covered by the
                // BattleMigrationMirror E2E test.
                Logger.Information("[BattleSync] Adopt-AI: {AI}/{Total} now AI-controlled across {Forms} formation(s)",
                    aiCount, adopted.Count, formations.Count);

                Logger.Information("[BattleSync] Adopted {Count} agent(s) from {Controller} ({Reason})",
                    adopted.Count, controllerId, reason);
            }
        });

        // We now own the departed controller's parties — pull our updated reserve from the server (the full
        // owned set at the current ledger pointers). ReinforcementFielder recovers newly-owned parties that had
        // no agents to adopt and continues them from those pointers. Runs even when nothing was adopted.
        RequestReserves();
    }

    // [Game thread] The player party (and hero) behind a controller id, from the session-scoped player
    // registry. The party is how own-party agents are identified across clients: puppets carry their origin
    // party (see PuppetSpawner), so comparing PartyBase references works on every client regardless of the
    // map event's current membership.
    private bool TryGetPlayerParty(string controllerId, out PartyBase party, out Hero hero)
    {
        party = null;
        hero = null;

        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero))
            hero = playerHero;

        if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty)) return false;
        party = mobileParty?.Party;
        return party != null;
    }

    // Whether an agent belongs to the given player's OWN party: the player's hero, or a troop whose origin
    // party is that player's party. The remote counterpart of OwnedAgentReplicator.IsOwnPartyAgent (which can
    // compare against PartyBase.MainParty locally); the hero check is a belt for a hero agent whose origin
    // party did not resolve at spawn.
    private static bool IsOwnPartyAgent(Agent agent, PartyBase playerParty, Hero playerHero)
    {
        if (playerHero != null && agent.Character is CharacterObject character && character.IsHero && character.HeroObject == playerHero)
            return true;
        return agent.Origin is CoopAgentOrigin origin && origin.Party == playerParty;
    }

    // An agent that withdraws with the retreating player rather than being adopted: an own-party troop, or the
    // horse one of them is riding (a horse has no origin party of its own — it follows its rider out).
    private static bool IsRetreatersAgent(Agent agent, PartyBase playerParty, Hero playerHero)
    {
        if (agent.IsMount)
            return agent.RiderAgent is Agent rider && IsOwnPartyAgent(rider, playerParty, playerHero);
        return IsOwnPartyAgent(agent, playerParty, playerHero);
    }

    // [Owner, game thread] Ask the server for our current owned reserve (after adopting a departed owner's
    // parties). The reply re-sets our suppliers at the ledger pointers.
    private void RequestReserves()
    {
        if (!session.HasInstance) return;
        var id = session.InstanceId;
        GameThread.RunSafe(() => relayNetwork.SendAll(new NetworkRequestBattleReserves(id, session.OwnControllerId)));
    }

    // Turn an inert puppet (driven only by replicated movement) into a real AI combatant under the host's
    // command: keep the formation slot its owner placed it in (mirrored at spawn) and hand it to the engine AI
    // so it maneuvers and fights like the host's own AI troops. The formation is set AI-controlled because in a
    // coop battle the host fights as a hero, not a general, so nothing would otherwise order it to engage.
    private void ConvertPuppetToHostAi(Agent agent)
    {
        // Fall back to the troop-class default only if the puppet has no formation yet.
        var formation = agent.Formation ?? formationAssigner.Assign(agent);
        formation?.SetControlledByAI(true);

        agent.Controller = AgentControllerType.AI;

        // Wake the AI the same way the NPC-release path does. Without this an adopted agent is AI-controlled
        // but NOT alarmed and holds stale enemy caches, so it ignores its formation's Charge order and stands
        // idle — the "allied NPCs don't move after host migration" bug. The ally side never goes through the
        // NPC release (which only frees the ENEMY side), so the adopt path must do the wake itself; only
        // combat troops reach this conversion (the adoption loop skips registered mounts), so the
        // CanWieldWeapon guard the NPC release uses is unnecessary here.
        AgentAiWaker.Wake(agent);
    }
}
