using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
                Agent affectorAgent = null;
                if (payload.What.AffectorAgentId != Guid.Empty
                    && registry.TryGetAgentInfo(payload.What.AffectorAgentId, out var affectorInfo))
                {
                    affectorAgent = affectorInfo.Agent;
                }

                var blow = CreateReplicatedBlow(payload.What, affectorAgent?.Index ?? -1);
                var killingBlow = payload.What.DeathAction >= 0
                    ? CreateReplicatedKillingBlow(blow, payload.What.DeathAction)
                    : default;
                blow.InflictedDamage = Math.Max(blow.InflictedDamage, (int)Math.Ceiling(agent.Health));
                var agentState = payload.What.Wounded ? AgentState.Unconscious : AgentState.Killed;

                BattleSpawnGate.RunWithReplicatedDeath(
                    agent,
                    affectorAgent,
                    killingBlow,
                    agentState,
                    () =>
                    {
                        using (new AllowedThread())
                        {
                            agent.RegisterBlow(blow, default);
                        }
                    });
            }

            // Deregister AFTER the kill, INSIDE this game-thread action. We receive this on the network thread,
            // so RunSafe queues the kill; removing on the network thread (outside the lambda) raced ahead of it,
            // and the re-check above then found the agent already gone and bailed — so the kill never ran and the
            // puppet stayed alive, removed but not killed, as an invincible, unroutable agent.
            registry.RemoveAgent(payload.What.AgentId);
            casualties.Forget(payload.What.AgentId);
        });
    }

    private static Blow CreateReplicatedBlow(NetworkBattleAgentDied message, int ownerId)
    {
        return new Blow(ownerId)
        {
            InflictedDamage = message.InflictedDamage,
            VictimBodyPart = message.VictimBodyPart,
        };
    }

    private static KillingBlow CreateReplicatedKillingBlow(Blow blow, int deathAction)
    {
        return new KillingBlow(blow, Vec3.Zero, Vec3.Zero, deathAction, 0);
    }
}
