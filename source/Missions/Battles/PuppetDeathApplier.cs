using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Peer-side death application for a coop battle: when an owner reports one of its agents died
/// (<see cref="NetworkBattleAgentDied"/>), kill our puppet of it and deregister.
/// </summary>
public interface IPuppetDeathApplier : IDisposable
{
}

/// <inheritdoc cref="IPuppetDeathApplier"/>
public class PuppetDeathApplier : IPuppetDeathApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetDeathApplier>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ICasualtyAttributionMap casualties;

    public PuppetDeathApplier(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ICasualtyAttributionMap casualties)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.casualties = casualties;

        messageBroker.Subscribe<NetworkBattleAgentDied>(Handle_NetworkBattleAgentDied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleAgentDied>(Handle_NetworkBattleAgentDied);
    }

    private void Handle_NetworkBattleAgentDied(MessagePayload<NetworkBattleAgentDied> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;
        Logger.Information("[DeathDiag] Received death broadcast for agent {AgentId}", payload.What.AgentId);

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.AgentId, out _))
            {
                Logger.Information("[DeathDiag] No registered puppet for {AgentId} — cannot kill it (its spawn was missed, or the id does not match)", payload.What.AgentId);
                return;
            }

            if (!registry.TryGetAgentInfo(payload.What.AgentId, out var info)) return;

            Agent agent = info.Agent;
            if (Mission.Current == null) return;
            Logger.Information("[DeathDiag] Killing puppet {AgentId}: agentPresent={Present}, health={Health}", payload.What.AgentId, agent != null, agent?.Health ?? -1f);
            if (agent != null && agent.Health > 0)
            {
                // Dismount first: MakeDead skips the dismount a real death does, so the horse would keep a link to
                // the dead rider and AVE in native Agent.Die when later killed. The horse itself is left
                // standing — a REGISTERED one dies through its OWN death broadcast (see AgentDeathReporter),
                // an unregistered one stays a local loose horse.
                if (agent.MountAgent != null)
                    agent.MountAgent = null;

                Agent affectorAgent = null;
                if (payload.What.AffectorAgentId != Guid.Empty
                    && registry.TryGetAgentInfo(payload.What.AffectorAgentId, out var affectorInfo))
                {
                    affectorAgent = affectorInfo.Agent;
                }

                var killingBlow = payload.What.KillingBlow;
                killingBlow.OwnerId = affectorAgent?.Index ?? -1;
                var deathAction = killingBlow.IsValid
                    ? new ActionIndexCache(killingBlow.DeathAction)
                    : ActionIndexCache.act_none;

                BattleSpawnGate.RunWithReplicatedDeath(
                    agent,
                    affectorAgent,
                    killingBlow,
                    () => agent.MakeDead(!payload.What.Wounded, deathAction));
            }

            // Deregister AFTER the kill, INSIDE this game-thread action. We receive this on the network thread,
            // so RunSafe queues the kill; removing on the network thread (outside the lambda) raced ahead of it,
            // and the re-check above then found the agent already gone and bailed — so MakeDead never ran and the
            // puppet stayed alive (removed but not killed → an invincible, unroutable agent).
            registry.RemoveAgent(payload.What.AgentId);
            casualties.Forget(payload.What.AgentId);
        });
    }
}
