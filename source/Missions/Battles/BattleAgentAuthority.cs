using GameInterface.Services.MapEvents;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The <see cref="IAgentAuthority"/> implementation for a coop field/siege battle, over the per-mission
/// <see cref="INetworkAgentRegistry"/> and the shared <see cref="IBattleSession"/>. One instance per battle,
/// constructed by <see cref="CoopBattleController"/>, shared by the read-path components (damage router, death /
/// rout reporters, siege crew snapshot) and installed into the static <see cref="BattleSpawnGate.AgentAuthority"/>
/// bridge so the DI-less GameInterface patches read the same answer.
/// </summary>
public class BattleAgentAuthority : IAgentAuthority
{
    private readonly INetworkAgentRegistry registry;
    private readonly IBattleSession session;

    public BattleAgentAuthority(INetworkAgentRegistry registry, IBattleSession session)
    {
        this.registry = registry;
        this.session = session;
    }

    /// <inheritdoc/>
    public bool IsMine(Agent agent) => registry.IsLocallyControlled(agent);

    /// <inheritdoc/>
    public bool IsMine(Guid agentId) => registry.IsLocallyControlled(agentId);

    /// <inheritdoc/>
    public string Owner(Agent agent)
        => registry.TryGetAgentInfo(agent, out var info) ? info.CurrentAuthority : null;

    /// <inheritdoc/>
    public bool IsPuppet(Agent agent)
    {
        if (agent == null) return false;

        // Registered → puppet iff another controller currently holds its authority.
        if (registry.TryGetAgentInfo(agent, out var info))
            return info.CurrentAuthority != session.OwnControllerId;

        // Unregistered human → the engine controller decides. A locally-simulated agent (AI/Player) takes the
        // blow; a None-controller agent is a puppet. This keeps blows suppressed on a troop that has just been
        // deregistered and is fading out of a withdraw (FadeOut is not instant), so peers watching it fade don't
        // diverge from a client that kills it locally.
        if (agent.IsHuman)
            return agent.Controller == AgentControllerType.None;

        // Unregistered mount → key off its rider's ownership (the old MountAuthorityProbe null-fallback). A
        // masterless horse, or one carrying our own (non-puppet) rider, has no ownership conflict — take the
        // blow locally; a horse under a puppet rider routes with it.
        var rider = agent.RiderAgent;
        return rider != null && rider.Controller == AgentControllerType.None;
    }
}
