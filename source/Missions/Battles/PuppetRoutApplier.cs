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

            // IsActive() guards the native FadeOut: a puppet already removed (duplicate rout, disconnect
            // adoption, or teardown) keeps a non-null registry entry with stale Health > 0, and FadeOut's
            // GetPtr() then access-violates. Only fade the mount when it too is still active (its own
            // FadeOut AVEs on a torn-down horse); a leftover riderless horse is handled by the mount sync.
            if (agent != null && agent.IsActive() && agent.Health > 0)
            {
                bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
                agent.FadeOut(true, hideMount);
            }

            // Deregister AFTER the despawn, inside this game-thread action — same ordering rationale as
            // PuppetDeathApplier.
            registry.RemoveAgent(payload.What.AgentId);
            casualties.Forget(payload.What.AgentId);
        });
    }
}
