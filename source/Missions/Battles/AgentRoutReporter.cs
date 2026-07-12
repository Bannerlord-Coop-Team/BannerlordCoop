using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Serilog;
using System;

namespace Missions.Battles;

/// <summary>
/// Owner-side rout reporting for a coop battle: when one of OUR authoritative agents routs out
/// (<see cref="BattleAgentRouted"/>, published by <c>BattleAgentRoutedPatch</c>), tell every client to
/// despawn its puppet of it and drop the agent from the registry so the movement handler stops
/// broadcasting it. No roster casualty is sent — routed troops survive the battle.
/// </summary>
public interface IAgentRoutReporter : IDisposable
{
}

/// <inheritdoc cref="IAgentRoutReporter"/>
public class AgentRoutReporter : IAgentRoutReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentRoutReporter>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly ICasualtyAttributionMap casualties;

    public AgentRoutReporter(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session,
        ICasualtyAttributionMap casualties)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.casualties = casualties;

        messageBroker.Subscribe<BattleAgentRouted>(Handle_BattleAgentRouted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleAgentRouted>(Handle_BattleAgentRouted);
    }

    private void Handle_BattleAgentRouted(MessagePayload<BattleAgentRouted> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.Agent, out var info)) return;
            if (info.CurrentAuthority != session.OwnControllerId) return;

            Logger.Information("[DeathDiag] Broadcasting rout of agent {AgentId} to the battle mesh", info.AgentId);
            network.SendAll(new NetworkBattleAgentRouted(info.AgentId));

            casualties.Forget(info.AgentId);
            registry.RemoveAgent(info.AgentId);
        });
    }
}
