using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Peer-side rout application for a coop battle: when an owner reports one of its agents routed out
/// (<see cref="NetworkBattleAgentRouted"/>), despawn our puppet of it and deregister. Without this the
/// puppet stays alive here and the local live-agent depletion count never reaches zero.
/// </summary>
public interface IPuppetRoutApplier : IDisposable
{
}

/// <inheritdoc cref="IPuppetRoutApplier"/>
public class PuppetRoutApplier : IPuppetRoutApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<PuppetRoutApplier>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ICasualtyAttributionMap casualties;

    public PuppetRoutApplier(
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        ICasualtyAttributionMap casualties)
    {
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.casualties = casualties;

        messageBroker.Subscribe<NetworkBattleAgentRouted>(Handle_NetworkBattleAgentRouted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleAgentRouted>(Handle_NetworkBattleAgentRouted);
    }

    private void Handle_NetworkBattleAgentRouted(MessagePayload<NetworkBattleAgentRouted> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.AgentId, out var info)) return;

            Agent agent = info.Agent;
            if (Mission.Current == null) return;

            if (agent != null && agent.Health > 0)
            {
                agent.FadeOut(true, true);
            }

            // Deregister AFTER the despawn, inside this game-thread action — same ordering rationale as
            // PuppetDeathApplier.
            registry.RemoveAgent(payload.What.AgentId);
            casualties.Forget(payload.What.AgentId);
        });
    }
}
