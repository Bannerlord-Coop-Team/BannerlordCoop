using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// [Game thread] Apply deaths that arrived before their deployment-buffered puppets registered.
    /// </summary>
    void DrainPendingDeaths();
}

/// <inheritdoc cref="IPuppetDeathApplier"/>
public class PuppetDeathApplier : IPuppetDeathApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetDeathApplier>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ICasualtyAttributionMap casualties;
    private readonly Dictionary<Guid, NetworkBattleAgentDied> pendingDeaths =
        new Dictionary<Guid, NetworkBattleAgentDied>();

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
        pendingDeaths.Clear();
    }

    private void Handle_NetworkBattleAgentDied(MessagePayload<NetworkBattleAgentDied> payload)
    {
        Logger.Information("[DeathDiag] Received death broadcast for agent {AgentId}", payload.What.AgentId);

        GameThread.RunSafe(() =>
        {
            if (!TryApplyDeath(payload.What))
            {
                pendingDeaths[payload.What.AgentId] = payload.What;
                Logger.Information("[DeathDiag] Deferring death of {AgentId} until its puppet registers", payload.What.AgentId);
            }
        });
    }

    public void DrainPendingDeaths()
    {
        if (pendingDeaths.Count == 0) return;

        var deaths = new List<NetworkBattleAgentDied>(pendingDeaths.Values);
        foreach (var death in deaths)
        {
            if (TryApplyDeath(death))
                pendingDeaths.Remove(death.AgentId);
        }
    }

    private bool TryApplyDeath(NetworkBattleAgentDied death)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(death.AgentId, out var info)) return false;
        if (Mission.Current == null) return false;

        Agent agent = info.Agent;
        Logger.Information("[DeathDiag] Killing puppet {AgentId}: agentPresent={Present}, health={Health}", death.AgentId, agent != null, agent?.Health ?? -1f);
        if (agent != null && agent.Health > 0)
        {
            Agent affectorAgent = null;
            if (death.AffectorAgentId != Guid.Empty
                && registry.TryGetAgentInfo(death.AffectorAgentId, out var affectorInfo))
            {
                affectorAgent = affectorInfo.Agent;
            }

            var blow = CreateReplicatedBlow(death, affectorAgent?.Index ?? -1);
            var killingBlow = death.DeathAction >= 0
                ? CreateReplicatedKillingBlow(blow, death.DeathAction)
                : default;
            blow.InflictedDamage = Math.Max(blow.InflictedDamage, (int)Math.Ceiling(agent.Health));
            var agentState = death.Wounded ? AgentState.Unconscious : AgentState.Killed;

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

        // Deregister after the game-thread kill. Removing on the poll thread before the queued apply would
        // make the registry lookup fail and leave the puppet alive but unregistered.
        registry.RemoveAgent(death.AgentId);
        casualties.MarkDeparted(death.AgentId);
        return true;
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
