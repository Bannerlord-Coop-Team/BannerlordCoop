using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Missions.Services.Network;
using SandBox;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Locations;

/// <summary>
/// Owns the departure fork of a settlement location mission (SR-014/SR-015). On ANY member's
/// departure every remaining client despawns that controller's PLAYER puppet only — its NPC puppets
/// (the ones recorded in the binding map) stay on the field awaiting adoption. On promotion
/// (<see cref="LocationHostMigrated"/>, published only on the promoted client) the new host adopts
/// the previous host's NPCs in place: authority transfer, interpolation forget (a stale interpolation
/// target pins an adopted agent — the battle-migration lesson), then settlement-AI re-creation from
/// the LOCAL roster entry the puppet's origin already points at (SR-030, V5). Mirrors the generic
/// halves of <c>BattleAuthorityMigrator</c>; there is no withdrawn-own-party split — a location
/// "party" is just the player's body, which always despawns.
/// </summary>
public interface ILocationAuthorityMigrator : System.IDisposable
{
}

/// <inheritdoc cref="ILocationAuthorityMigrator"/>
public class LocationAuthorityMigrator : ILocationAuthorityMigrator
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationAuthorityMigrator>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ILocationSession session;
    private readonly ILocationAgentBindingMap bindingMap;
    private readonly IMissionContext missionContext;

    public LocationAuthorityMigrator(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ILocationSession session,
        ILocationAgentBindingMap bindingMap,
        IMissionContext missionContext)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.bindingMap = bindingMap;
        this.missionContext = missionContext;

        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Subscribe<LocationHostMigrated>(Handle_LocationHostMigrated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
        messageBroker.Unsubscribe<LocationHostMigrated>(Handle_LocationHostMigrated);
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        DespawnPlayerPuppet(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        DespawnPlayerPuppet(payload.What.ControllerId, payload.What.InstanceId);
    }

    // [All remaining clients] A member departed: despawn ITS PLAYER agent only. Its NPC puppets (every
    // registered agent with a binding-map record) stay — they belong to the promoted successor
    // (SR-015). This replaces the generic AgentMovementHandler.RemoveControllerParty sweep, which is
    // skipped for location missions exactly so it cannot fade the NPCs out with their departing host.
    internal void DespawnPlayerPuppet(string controllerId, string instanceId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (instanceId != null && instanceId != session.InstanceId) return;
        if (session.IsOwn(controllerId)) return; // our own departure tears the whole mission down

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            int despawned = 0;
            foreach (var info in registry.GetAgents(controllerId))
            {
                if (bindingMap.TryGet(info.AgentId, out _)) continue; // an NPC — awaits adoption

                var agent = info.Agent;
                if (agent != null && agent.IsActive() && agent.Health > 0)
                {
                    bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
                    agent.FadeOut(false, hideMount);
                }

                registry.RemoveAgent(info.AgentId);
                despawned++;
            }

            if (despawned > 0)
                Logger.Information("[LocationSync] Despawned {Count} player agent(s) of departed {Controller}", despawned, controllerId);
        }, context: nameof(DespawnPlayerPuppet));
    }

    // [Promoted host] The previous host departed and the server promoted us — adopt its NPCs in place
    // (SR-014). Published only on the promoted client, so no host check here.
    private void Handle_LocationHostMigrated(MessagePayload<LocationHostMigrated> payload)
    {
        if (payload.What.InstanceId != session.InstanceId) return;

        AdoptAgentsFrom(payload.What.PreviousHostControllerId, "host migration");

        // The departed host may itself have inherited NPCs through an earlier migration — those are
        // still keyed to older absent controllers here. Sweep every absent authority so nothing stays
        // frozen. Idempotent: once adopted, the agents are keyed to us.
        SweepAgentsOfAbsentControllers(payload.What.PreviousHostControllerId);
    }

    private void SweepAgentsOfAbsentControllers(string previousHost)
    {
        var present = new HashSet<string>(missionContext.ControllersInMission);

        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            if (string.IsNullOrEmpty(controllerId)) continue;
            if (controllerId == previousHost) continue; // the migration adoption already took these
            if (session.IsOwn(controllerId)) continue;
            if (present.Contains(controllerId)) continue; // still connected — its owner drives them

            AdoptAgentsFrom(controllerId, "location migration orphan sweep");
        }
    }

    // Take over the NPCs owned by the departed controller: move authority to us (the movement poller
    // then broadcasts them) and revive each puppet's settlement AI from the LOCAL roster entry its
    // origin points at — the native sequence (CampaignAgentComponent.CreateAgentNavigator +
    // entry.AddBehaviors, V5). Other peers keep them as puppets that now follow OUR movement (their
    // movement lookup is scope+id keyed, which survives the authority transfer).
    private void AdoptAgentsFrom(string controllerId, string reason)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (session.IsOwn(controllerId)) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            var adopted = new List<CoopAgentInfo>();
            foreach (var info in registry.GetAgents(controllerId))
            {
                // A lingering non-NPC entry (the departed player's own body whose despawn we somehow
                // missed) is not ours to adopt; DespawnPlayerPuppet owns it.
                if (!bindingMap.TryGet(info.AgentId, out _)) continue;
                adopted.Add(info);
            }

            if (adopted.Count == 0) return;

            foreach (var info in adopted)
                registry.TryTransferAuthority(session.OwnControllerId, info.AgentId);

            if (Mission.Current == null) return;

            var interpolator = coopMissionComponent.AgentMovementHandler.Interpolator;
            int revived = 0;
            int stationary = 0;
            foreach (var info in adopted)
            {
                var agent = info.Agent;
                if (agent == null || !agent.IsActive()) continue;

                // Stop reconciling the agent toward its former owner's last-reported position — a
                // stale interpolation target overrides the AI and pins the agent in place.
                interpolator.Forget(agent);

                if (ReviveSettlementAi(agent)) revived++;
                else stationary++;
            }

            Logger.Information("[LocationSync] Adopted {Count} NPC(s) from {Controller} ({Reason}): {Revived} revived, {Stationary} stationary fallback",
                adopted.Count, controllerId, reason, revived, stationary);
        }, context: nameof(AdoptAgentsFrom));
    }

    // [Game thread] Turn an inert puppet into a locally simulated settlement NPC. Humans get the
    // native AI stack re-created from their roster entry; an unresolvable entry (or an animal) falls
    // back to plain engine AI — stationary for humans (SR-031), native idle wandering for animals.
    private bool ReviveSettlementAi(Agent agent)
    {
        // An adopted MOUNT (scene horses) only changes authority — it is not a simulated combatant
        // and must not get an engine AI controller, mirroring battle mount adoption.
        if (agent.IsMount) return true;

        agent.Controller = AgentControllerType.AI;
        agent.SetIsAIPaused(false);

        if (!agent.IsHuman) return true; // non-mount animals need no navigator

        var entry = CampaignMission.Current?.Location?.GetLocationCharacter(agent.Origin);
        if (entry == null)
        {
            Logger.Warning("[LocationSync] Adopted NPC {Agent} has no local roster entry — leaving it as a stationary AI", agent.Index);
            return false;
        }

        // V5: the exact native spawn tail. Every campaign-mission agent gets a CampaignAgentComponent
        // at creation (CampaignMissionComponent.OnAgentCreated), but guard anyway.
        var component = agent.GetComponent<CampaignAgentComponent>();
        if (component == null)
        {
            component = new CampaignAgentComponent(agent);
            agent.AddComponent(component);
        }

        if (component.AgentNavigator == null)
            component.CreateAgentNavigator(entry);
        entry.AddBehaviors(agent);
        return true;
    }
}
