using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Ownership changes when a player leaves a coop battle. A graceful leave is a RETREAT: the player withdraws,
/// so their own-party troops despawn on every client. A disconnect is NOT a retreat: the host adopts the
/// dropped player's troops (authority + AI control) so they keep fighting instead of vanishing —
/// server-mediated, so it fires even on a silent P2P drop. And when the departed player WAS the host, the
/// server promotes a successor, which adopts the old host's orphaned agents — everything on a disconnect; on
/// a retreat only the AI it was running (enemy side + allied NPC parties), while its own party still withdraws.
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

    // Hosts that RETREATED (graceful leave) — read when the promotion lands so the adoption knows to leave the
    // retreater's own-party troops to the despawn instead of adopting them. Only touched from broker handlers,
    // which all run on the relay's receive thread, so no locking. Entries are consumed by the promotion and
    // cleared if the controller re-enters (a later drop of the same controller must not be treated as a retreat).
    private readonly HashSet<string> retreatedHosts = new HashSet<string>();

    public BattleAuthorityMigrator(
        INetwork relayNetwork,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner)
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

    // A controller (re-)entered the instance: any recorded retreat of it is history — a later departure must
    // be judged on its own, not as a leftover retreat.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        retreatedHosts.Remove(payload.What.ControllerId);
    }

    // A graceful leave is a RETREAT: the player withdraws, so their own troops despawn on every client — the
    // OPPOSITE of a disconnect, where the host adopts them so they keep fighting.
    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        var controllerId = payload.What.ControllerId;
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;

        // A retreating HOST: the promoted successor adopts the AI it was running (the enemy side + allied NPC
        // parties) via Handle_BattleHostMigrated — but its OWN party still retreats, so despawn exactly those
        // troops, on every client. Despawn and adoption previously raced over the SAME agents (two independent
        // queued game-thread actions touching the full set), which crashed on a host retreat; scoping this
        // despawn to the own-party agents and making the adoption skip them keeps the two sets DISJOINT, so
        // the order they run in no longer matters.
        if (session.IsHostController(controllerId))
        {
            retreatedHosts.Add(controllerId);
            Logger.Information("[BattleSync] Host {Controller} retreated — despawning its own party; migration adopts the AI it ran", controllerId);
            DespawnOwnPartyTroops(controllerId);
            return;
        }

        // A NON-host retreat: withdraw only that player's OWN player-side troops; the host keeps running the AI.
        DespawnControllerTroops(controllerId);
    }

    // A disconnect (ungraceful drop) is NOT a retreat: the host adopts the dropped player's troops so they
    // keep fighting (or, on a host drop, a successor is promoted).
    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        HandlePeerGone(payload.What.ControllerId, payload.What.InstanceId, "disconnected");
    }

    // [All clients] Withdraw a retreating HOST's own-party troops: its hero and the troops of the party it
    // leads, identified by OWNERSHIP (the agent's origin party), NOT by battle side — a host also fields
    // allied NPC parties on the player side, and those must keep fighting under the promoted successor.
    // FadeOut (not Die/MakeDead) so it is a withdrawal, not a casualty — the player keeps these troops on the
    // map (the server forgot its reserve on the retreat, so a rejoin re-flattens and re-spawns them fresh).
    private void DespawnOwnPartyTroops(string controllerId)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            if (!TryGetPlayerParty(controllerId, out var playerParty, out var playerHero))
            {
                // Can't tell the retreater's own troops from the AI parties it fielded — despawning the wrong
                // set is worse than keeping them fighting, so leave everything to the adoption (pre-fix behavior).
                Logger.Warning("[BattleSync] Cannot resolve the party of retreating host {Controller}; its troops will be adopted instead of despawned", controllerId);
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
                Logger.Information("[BattleSync] Despawned {Count} retreating own-party troop(s) of host {Controller}", despawned, controllerId);
        });
    }

    // [All clients] Withdraw a retreating NON-host's troops — only its player-SIDE agents. A non-host only
    // ever owns its own party, so the side filter is enough here (the ownership test above is for hosts).
    // Our own retreat tears the mission down (skip self); other clients drop its puppets. FadeOut (not
    // Die/MakeDead) so it is a withdrawal, not a casualty — the player keeps these troops on the map.
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

            if (Mission.Current.PlayerTeam == null)
            {
                Logger.Error("PlayerTeam was not set");
                return;
            }

            var playerSide = Mission.Current.PlayerTeam.Side;
            int despawned = 0;
            var despawnedRiders = new HashSet<Agent>();
            foreach (var info in troops)
            {
                var agent = info.Agent;
                if (agent == null || agent.IsMount || agent.Team == null || agent.Team.Side != playerSide)
                    continue;

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
                Logger.Information("[BattleSync] Despawned {Count} retreating troop(s) of {Controller}", despawned, controllerId);
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

    // [Host] A player left/dropped from this battle. Their troops must not vanish: the current host adopts
    // them. Only the host acts (a non-host ignores it; on a host departure the server promotes a successor,
    // which adopts via Handle_BattleHostMigrated instead).
    private void HandlePeerGone(string controllerId, string goneInstanceId, string reason)
    {
        if (goneInstanceId != null && goneInstanceId != session.InstanceId) return; // a different instance's churn
        if (!session.IsLocalHost) return;

        AdoptAgentsFrom(controllerId, reason, wasRetreat: false);
    }

    // [New host] The previous host departed and the server promoted us — adopt its orphaned agents so the
    // battle continues under us. Published only to the promoted client, so no host check here. Consumes the
    // retreat record: on a retreat the old host's own party withdraws (DespawnOwnPartyTroops) and only the AI
    // it ran is adopted.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        if (payload.What.MapEventId != session.InstanceId) return;

        var previousHost = payload.What.PreviousHostControllerId;
        AdoptAgentsFrom(previousHost, "host migration", wasRetreat: retreatedHosts.Remove(previousHost));

        // If the battle was already live when we were promoted, release the NPC AI we just adopted — a still-
        // deploying new host has AI ticking off, which would otherwise hold them frozen even though they were
        // moving under the previous host. If the battle was not live yet, this is a no-op (the gate holds).
        deployment.OnPromotedToHost();
    }

    // Take over the agents owned by the departed controller: move authority to us (so the movement poller
    // broadcasts them and the death/casualty path owns them — their attribution was captured at spawn) and
    // convert each inert puppet into a host AI combatant. Other peers keep them as puppets that follow our
    // movement. On a RETREAT the departed host's own-party troops withdraw instead (despawned by
    // DespawnOwnPartyTroops on every client), so they are excluded here — the disjoint sets are what make the
    // despawn and this adoption race-free.
    private void AdoptAgentsFrom(string controllerId, string reason, bool wasRetreat)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (session.IsOwn(controllerId)) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            PartyBase retreatedParty = null;
            Hero retreatedHero = null;
            if (wasRetreat && !TryGetPlayerParty(controllerId, out retreatedParty, out retreatedHero))
                Logger.Warning("[BattleSync] Cannot resolve the party of retreated host {Controller}; adopting all of its agents", controllerId);

            var adopted = new List<CoopAgentInfo>();
            foreach (var info in registry.GetAgents(controllerId))
            {
                // A retreating host's own-party troop — it withdraws (despawned), so it is not ours to adopt.
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
        // owned set at the current ledger pointers) so we can spawn their reinforcements from where the
        // departed owner left off. Runs even with no on-field agents adopted (reserve may still be unspawned).
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
