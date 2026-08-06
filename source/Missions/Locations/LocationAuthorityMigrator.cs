using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Missions.Services.Network;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using SandBox.Objects.AnimationPoints;
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
    /// <summary>
    /// [Game thread] Adopt a single just-spawned puppet whose owner has already departed: a retained
    /// old-host record applied AFTER the promotion (buffered by budget or mission load). The bulk
    /// adoption on <see cref="Missions.Messages.LocationHostMigrated"/> ran before this record
    /// spawned, so the spawner hands late arrivals over one by one.
    /// </summary>
    void AdoptSpawnedPuppet(Agent agent, System.Guid agentId);
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
    private readonly GameInterface.Services.Locations.Conversations.ILocationNpcHoldRegistry holdRegistry;

    public LocationAuthorityMigrator(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ILocationSession session,
        ILocationAgentBindingMap bindingMap,
        IMissionContext missionContext,
        GameInterface.Services.Locations.Conversations.ILocationNpcHoldRegistry holdRegistry)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.bindingMap = bindingMap;
        this.missionContext = missionContext;
        this.holdRegistry = holdRegistry;

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

                if (ReviveSettlementAi(agent, info.AgentId)) revived++;
                else stationary++;

                ReapplyConversationHold(agent, info.AgentId);
            }

            Logger.Information("[LocationSync] Adopted {Count} NPC(s) from {Controller} ({Reason}): {Revived} revived, {Stationary} stationary fallback",
                adopted.Count, controllerId, reason, revived, stationary);
        }, context: nameof(AdoptAgentsFrom));
    }

    public void AdoptSpawnedPuppet(Agent agent, System.Guid agentId)
    {
        if (agent == null || agentId == System.Guid.Empty) return;

        var registry = coopMissionComponent.AgentRegistry;
        registry.TryTransferAuthority(session.OwnControllerId, agentId);
        coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
        ReviveSettlementAi(agent, agentId);
        ReapplyConversationHold(agent, agentId);
        Logger.Information("[LocationSync] Late-adopted NPC {AgentId} spawned after the migration", agentId);
    }

    // [Game thread] An adopted NPC that a remote player currently holds the conversation lock on
    // must stay paused (SR-040) — the hold broadcast predates our authority, so the registry every
    // client maintains is the only signal a successor has.
    private void ReapplyConversationHold(Agent agent, System.Guid agentId)
    {
        if (agent == null || !agent.IsHuman) return;
        if (!bindingMap.TryGet(agentId, out var binding) || binding.RosterEntry == null) return;

        if (holdRegistry.IsHeld(binding.RosterEntry.LocationId, binding.RosterEntry.CharacterId))
        {
            agent.SetIsAIPaused(true);
            Logger.Information("[LocationSync] Adopted NPC {AgentId} is conversation-held — keeping it paused", agentId);
        }
    }

    // [Game thread] Turn an inert puppet into a locally simulated settlement NPC. Humans get the
    // native AI stack re-created from their roster entry; an unresolvable entry (or an animal) falls
    // back to plain engine AI — stationary for humans (SR-031), native idle wandering for animals.
    private bool ReviveSettlementAi(Agent agent, System.Guid agentId)
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

        // V5: the exact native spawn tail. The puppet spawner already created a roster-bound
        // navigator at spawn (carry prefabs + special item attached there), so the create below is
        // normally a no-op and only the behavior groups are new; the guards cover agents from before
        // a mid-mission code path change or a stand-in that somehow reached here.
        var component = agent.GetComponent<CampaignAgentComponent>();
        if (component == null)
        {
            component = new CampaignAgentComponent(agent);
            agent.AddComponent(component);
        }

        if (component.AgentNavigator == null)
            component.CreateAgentNavigator(entry);
        entry.AddBehaviors(agent);
        ReconnectAdoptedPointUse(agent, component.AgentNavigator);
        return true;
    }

    // [Game thread] Keep an adopted mid-performance NPC in its seat: wire the fresh navigator and
    // wander behavior to the point the agent is ALREADY using — the exact steady state the native
    // pipeline would have produced had this client sent it there (SR-043). Without this, the first
    // behavior tick sees NoTarget and either retargets (dragging the sitter through the furniture
    // toward a random new point) or calls SetTarget(null), whose DisableScriptedMovement releases a
    // still-seated agent's locked seat frame and breaks its facing; native never reaches either path
    // because a native sitter always carries TargetUsableMachine. IDetachment.AddAgent is
    // deliberately NOT called — it only assigns VACANT points and would shuffle the agent to a
    // different seat of the same machine. The natural leave later flows through the untouched native
    // path (behavior retarget → IDetachment.RemoveAgent → StopUsingGameObjectMT), which the host's
    // point-usage poll then replicates.
    private void ReconnectAdoptedPointUse(Agent agent, AgentNavigator navigator)
    {
        if (navigator == null || !(agent.CurrentlyUsedGameObject is StandingPoint usedPoint)) return;

        var machine = FindOwningMachine(usedPoint);
        if (machine == null)
        {
            // Not a machine's point (unexpected for settlement performances): stand down cleanly
            // NOW, before anything ticks, rather than let the fresh AI fight the seat.
            agent.StopUsingGameObject(isSuccessful: true);
            Logger.Warning("[LocationSync] Adopted NPC {Agent} used a machine-less point — stood it down for AI takeover", agent.Index);
            return;
        }

        navigator.TargetUsableMachine = machine;
        navigator._agentState = AgentNavigator.NavigationState.UseMachine;
        navigator._targetBehavior = machine.CreateAIBehaviorObject();

        foreach (var group in navigator._behaviorGroups)
            foreach (var behavior in group.Behaviors)
                behavior.SetCustomWanderTarget(machine);

        // WalkingBehavior tracks its last target separately; left null its next tick would
        // SetTarget(null) the seated agent right back off the point.
        var walking = navigator.GetBehaviorGroup<DailyBehaviorGroup>()?.GetBehavior<WalkingBehavior>();
        if (walking != null)
        {
            walking._lastTarget = machine;

            // SetCustomWanderTarget cleared the wander wait timer, and with most of the crowd on
            // points at migration (216 of 228 in the live repro) a FRESH full wait for everyone at
            // once is a synchronized transition drought — the square looks frozen for the first
            // minute-plus. The previous host's timers were mid-flight, so hand this agent a
            // BACKDATED one: uniform elapsed share of the point's own rolled duration, restoring
            // the steady-state leave cadence from the first post-migration tick. npc_idle-tagged
            // targets get no timer, exactly like the native arrival path.
            if (!machine.GameEntity.HasTag("npc_idle"))
            {
                float wait = usedPoint is AnimationPoint animationPoint
                    ? animationPoint.GetRandomWaitInSeconds()
                    : 10f;
                if (wait >= 0f)
                    walking._waitTimer = new TaleWorlds.Core.Timer(
                        Mission.Current.CurrentTime - wait * MBRandom.RandomFloat, wait);
            }
        }

        // Restore the native Using-state anchor — the AI-scripted seat frame AnimationPoint issues
        // when a native agent seats (and which never took effect on the Controller.None puppet) —
        // realigning in the same stroke whatever offset the agent accumulated as a puppet.
        var frame = usedPoint.GetUserFrameForAgent(agent);
        agent.ClearTargetFrame();
        agent.SetScriptedPositionAndDirection(
            ref frame.Origin,
            frame.Rotation.f.AsVec2.RotationInRadians,
            addHumanLikeDelay: false,
            Agent.AIScriptedFrameFlags.DoNotRun);
    }

    private static UsableMachine FindOwningMachine(StandingPoint point)
    {
        foreach (var missionObject in Mission.Current.MissionObjects)
        {
            if (missionObject is UsableMachine machine
                && machine.StandingPoints != null
                && machine.StandingPoints.Contains(point))
                return machine;
        }
        return null;
    }
}
