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
/// departure every remaining client despawns that controller's player and companion puppets — its NPC puppets
/// (the ones recorded in the binding map) stay on the field awaiting adoption. On promotion
/// (<see cref="LocationHostMigrated"/>) every peer transfers registry authority and the new host adopts
/// the previous host's NPCs in place: interpolation forget (a stale interpolation
/// target pins an adopted agent — the battle-migration lesson), then settlement-AI re-creation from
/// the LOCAL roster entry the puppet's origin already points at (SR-030, V5). Mirrors the generic
/// halves of <c>BattleAuthorityMigrator</c>; player and companion agents always despawn, while only
/// roster-bound ambient NPCs survive for adoption.
/// </summary>
public interface ILocationAuthorityMigrator : System.IDisposable
{
    /// <summary>
    /// [Game thread] Apply current authority to a just-spawned puppet whose owner already departed.
    /// Every peer corrects its registry; the promoted host also revives settlement AI.
    /// </summary>
    void ApplyLateSpawnedPuppet(Agent agent, System.Guid agentId);
}

/// <inheritdoc cref="ILocationAuthorityMigrator"/>
public class LocationAuthorityMigrator : ILocationAuthorityMigrator
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationAuthorityMigrator>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ILocationSession session;
    private readonly ILocationAgentBindingMap bindingMap;
    private readonly ILocationPartyAgentMap partyAgentMap;
    private readonly IMissionContext missionContext;
    private readonly GameInterface.Services.Locations.Conversations.ILocationNpcHoldRegistry holdRegistry;

    public LocationAuthorityMigrator(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ILocationSession session,
        ILocationAgentBindingMap bindingMap,
        ILocationPartyAgentMap partyAgentMap,
        IMissionContext missionContext,
        GameInterface.Services.Locations.Conversations.ILocationNpcHoldRegistry holdRegistry)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.bindingMap = bindingMap;
        this.partyAgentMap = partyAgentMap;
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
        DespawnPartyPuppets(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        DespawnPartyPuppets(payload.What.ControllerId, payload.What.InstanceId);
    }

    // [All remaining clients] A member departed: despawn its player and companion agents. Its NPC puppets (every
    // registered agent with a binding-map record) stay — they belong to the promoted successor
    // (SR-015). This replaces the generic AgentMovementHandler.RemoveControllerParty sweep, which is
    // skipped for location missions exactly so it cannot fade the NPCs out with their departing host.
    internal void DespawnPartyPuppets(string controllerId, string instanceId)
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
                bool hasNpcBinding = bindingMap.TryGet(info.AgentId, out _);
                if (partyAgentMap.ShouldAdoptAsNpc(info.AgentId, hasNpcBinding)) continue;

                var agent = info.Agent;
                coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
                registry.RemoveAgent(info.AgentId);

                // A party id can carry a stale NPC binding when a host's ambient spawn batch raced its
                // companion join record. It is still a departing party agent and must never be adopted.
                bindingMap.Forget(info.AgentId);

                if (agent != null && agent.Mission == Mission.Current && agent.IsActive() && agent.Health > 0)
                {
                    bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
                    agent.FadeOut(false, hideMount);
                }

                Logger.Information("[LocationSync] Despawned departed party agent {AgentId} ({Character}) from {Controller}",
                    info.AgentId, agent?.Character?.StringId ?? "<null>", controllerId);
                despawned++;
            }

            if (despawned > 0)
                Logger.Information("[LocationSync] Despawned {Count} player agent(s) of departed {Controller}", despawned, controllerId);
        }, context: nameof(DespawnPartyPuppets));
    }

    // [All remaining clients] Apply the server's host epoch to NPC authority. The promoted host also
    // revives the adopted agents' settlement AI.
    private void Handle_LocationHostMigrated(MessagePayload<LocationHostMigrated> payload)
    {
        if (payload.What.InstanceId != session.InstanceId) return;

        // The departed host may itself have inherited NPCs through an earlier migration — those are
        // still keyed to older absent controllers here. Adopt every absent authority in ONE batch:
        // paired AnimationPoints can stop users across authority keys, so separate batches could
        // strand a partner that an earlier batch had already processed.
        var absentControllers = new HashSet<string>();
        if (!string.IsNullOrEmpty(payload.What.PreviousHostControllerId))
            absentControllers.Add(payload.What.PreviousHostControllerId);

        var present = new HashSet<string>(missionContext.ControllersInMission);
        foreach (var controllerId in coopMissionComponent.AgentRegistry.GetControllerIds())
        {
            if (string.IsNullOrEmpty(controllerId)) continue;
            if (session.IsOwn(controllerId)) continue;
            if (present.Contains(controllerId)) continue;
            absentControllers.Add(controllerId);
        }

        if (!session.IsOwn(payload.What.NewHostControllerId))
        {
            TransferRemoteAuthority(
                absentControllers,
                payload.What.NewHostControllerId,
                payload.What.AuthorityRevision);
            return;
        }

        AdoptAgentsFrom(
            absentControllers,
            "host migration + orphan sweep",
            payload.What.AuthorityRevision);
    }

    private void TransferRemoteAuthority(
        IEnumerable<string> controllerIds,
        string newHostControllerId,
        long authorityRevision)
    {
        if (controllerIds == null || string.IsNullOrEmpty(newHostControllerId)) return;
        var controllers = new HashSet<string>(controllerIds);
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            foreach (var controllerId in controllers)
            {
                foreach (var info in registry.GetAgents(controllerId))
                {
                    bool hasNpcBinding = bindingMap.TryGet(info.AgentId, out _);
                    if (!partyAgentMap.ShouldAdoptAsNpc(info.AgentId, hasNpcBinding)) continue;
                    registry.TryTransferAuthority(
                        newHostControllerId,
                        info.AgentId,
                        authorityRevision);
                }
            }
        }, context: nameof(TransferRemoteAuthority));
    }

    // Take over the NPCs owned by the departed controller: move authority to us (the movement poller
    // then broadcasts them) and revive each puppet's settlement AI from the LOCAL roster entry its
    // origin points at — the native sequence (CampaignAgentComponent.CreateAgentNavigator +
    // entry.AddBehaviors, V5). Other peers keep them as puppets that now follow OUR movement (their
    // movement lookup is scope+id keyed, which survives the authority transfer).
    private void AdoptAgentsFrom(
        IEnumerable<string> controllerIds,
        string reason,
        long? authorityRevision = null)
    {
        if (controllerIds == null) return;

        var controllers = new HashSet<string>();
        foreach (var controllerId in controllerIds)
        {
            if (string.IsNullOrEmpty(controllerId) || session.IsOwn(controllerId)) continue;
            controllers.Add(controllerId);
        }
        if (controllers.Count == 0) return;

        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            var adopted = new List<CoopAgentInfo>();
            foreach (var controllerId in controllers)
            {
                foreach (var info in registry.GetAgents(controllerId))
                {
                    // Party identity wins over a binding produced by any racing ambient spawn record.
                    // Player and companion puppets always despawn with their controller and are never adopted.
                    bool hasNpcBinding = bindingMap.TryGet(info.AgentId, out _);
                    if (!partyAgentMap.ShouldAdoptAsNpc(info.AgentId, hasNpcBinding))
                    {
                        if (hasNpcBinding && partyAgentMap.Contains(info.AgentId))
                        {
                            Logger.Warning("[LocationSync] Refused host-migration adoption for party agent {AgentId}",
                                info.AgentId);
                        }
                        continue;
                    }
                    adopted.Add(info);
                }
            }

            if (adopted.Count == 0) return;

            foreach (var info in adopted)
            {
                if (authorityRevision.HasValue)
                {
                    registry.TryTransferAuthority(
                        session.OwnControllerId,
                        info.AgentId,
                        authorityRevision.Value);
                }
                else
                {
                    registry.TryTransferAuthority(session.OwnControllerId, info.AgentId);
                }
            }

            if (Mission.Current == null) return;

            var interpolator = coopMissionComponent.AgentMovementHandler.Interpolator;
            var adoptedHumans = new List<(Agent Agent, AgentNavigator Navigator)>();
            int revived = 0;
            int stationary = 0;
            foreach (var info in adopted)
            {
                var agent = info.Agent;
                if (agent == null || !agent.IsActive()) continue;

                // Stop reconciling the agent toward its former owner's last-reported position — a
                // stale interpolation target overrides the AI and pins the agent in place.
                interpolator.Forget(agent);

                if (ReviveSettlementAi(agent, info.AgentId, reconnectPointUse: false)) revived++;
                else stationary++;

                // Include stationary-fallback humans too. Paired AnimationPoint shutdown can stop
                // them even without a navigator; they still need their point lifecycle restarted.
                if (agent.IsHuman)
                    adoptedHumans.Add((agent, agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator));

                ReapplyConversationHold(agent, info.AgentId);
            }

            // All users must be AI before any paired AnimationPoint is stopped: stopping a pair lead
            // stops its AI partners too. Snapshot + stop the entire batch, restart pair leads first,
            // then reconnect every navigator so registry order cannot strand a paired user.
            RestartAndReconnectAdoptedPointUses(adoptedHumans);

            Logger.Information("[LocationSync] Adopted {Count} NPC(s) from {Controllers} ({Reason}): {Revived} revived, {Stationary} stationary fallback",
                adopted.Count, string.Join(", ", controllers), reason, revived, stationary);
        }, context: nameof(AdoptAgentsFrom));
    }

    public void ApplyLateSpawnedPuppet(Agent agent, System.Guid agentId)
    {
        if (agent == null || agentId == System.Guid.Empty) return;
        if (partyAgentMap.Contains(agentId))
        {
            Logger.Warning("[LocationSync] Refused late NPC adoption for party agent {AgentId}", agentId);
            return;
        }

        string hostControllerId = session.HostControllerId;
        long authorityRevision = session.HostEpoch - 1L;
        if (string.IsNullOrEmpty(hostControllerId) || authorityRevision < 0) return;

        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryTransferAuthority(hostControllerId, agentId, authorityRevision)) return;
        if (!session.IsLocalHost) return;

        coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
        // The spawner has just applied any catch-up point use from its canonical frame. Preserve that
        // single fresh lifecycle and only reconnect its newly-live AI navigator; do not stop/reuse it.
        ReviveSettlementAi(agent, agentId, reconnectPointUse: true);
        ReapplyConversationHold(agent, agentId);
        Logger.Information(
            "[LocationSync] Late-adopted NPC {AgentId} spawned after the migration at revision {Revision}",
            agentId,
            authorityRevision);
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
    private bool ReviveSettlementAi(Agent agent, System.Guid agentId, bool reconnectPointUse)
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
        if (reconnectPointUse)
            ReconnectAdoptedPointUse(agent, component.AgentNavigator);
        return true;
    }

    // [Game thread] Pair-safe bulk restart. AnimationPoint.OnUseStopped may stop every AI user on
    // its paired points, so restarting users one at a time makes the result depend on registry order.
    // Snapshot first, stop every animation user, restart pair activators before their child points,
    // and only then reconnect every navigator.
    private void RestartAndReconnectAdoptedPointUses(
        List<(Agent Agent, AgentNavigator Navigator)> revivedAgents)
    {
        var pointUsers = new List<(Agent Agent, AgentNavigator Navigator, StandingPoint Point)>();
        foreach (var revived in revivedAgents)
        {
            if (revived.Agent.CurrentlyUsedGameObject is StandingPoint point)
                pointUsers.Add((revived.Agent, revived.Navigator, point));
        }

        foreach (var user in pointUsers)
        {
            if (user.Point is AnimationPoint
                && ReferenceEquals(user.Agent.CurrentlyUsedGameObject, user.Point))
                user.Agent.StopUsingGameObject(isSuccessful: true);
        }

        // ActivatePairs.OnUse enables its paired child points. Starting activators first keeps a
        // child from being reused while the lead still has it disabled from the stop phase.
        RestartAdoptedAnimationPoints(pointUsers, activatePairs: true);
        RestartAdoptedAnimationPoints(pointUsers, activatePairs: false);

        foreach (var user in pointUsers)
            ReconnectAdoptedPointUse(user.Agent, user.Navigator);
    }

    private void RestartAdoptedAnimationPoints(
        List<(Agent Agent, AgentNavigator Navigator, StandingPoint Point)> pointUsers,
        bool activatePairs)
    {
        foreach (var user in pointUsers)
        {
            if (!(user.Point is AnimationPoint animationPoint)
                || animationPoint.ActivatePairs != activatePairs)
                continue;

            LocationPointUseLifecycle.RestartFromCanonicalFrame(user.Agent, animationPoint);
            Logger.Debug("[LocationSync] Adopted NPC {Agent} restarted point {PointId} arrival {Arrival}",
                user.Agent.Index, animationPoint.Id.Id, animationPoint.ArriveAction);
        }
    }

    // [Game thread] Wire the navigator and wander behavior to the point the agent is using. Without
    // this, the first behavior tick sees NoTarget and either retargets or calls SetTarget(null),
    // releasing the point immediately. IDetachment.AddAgent is deliberately NOT called — it only
    // assigns VACANT points and would shuffle the agent to a different seat of the same machine. The
    // natural leave later flows through behavior retarget → IDetachment.RemoveAgent →
    // StopUsingGameObjectMT, which the host's point-usage poll then replicates.
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
