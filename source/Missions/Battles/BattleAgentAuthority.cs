using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Answers agent ownership for one battle from its agent registry and this client's controller id.</summary>
internal class BattleAgentAuthority : IBattleAgentAuthority
{
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IBattleSession session;

    // Binds the answers to the battle's agent registry and session.
    public BattleAgentAuthority(INetworkAgentRegistry agentRegistry, IBattleSession session)
    {
        this.agentRegistry = agentRegistry;
        this.session = session;
    }

    /// <inheritdoc/>
    public bool? IsMountRemotelyOwned(Agent mount)
    {
        if (!agentRegistry.TryGetAgentInfo(mount, out var info)) return null;

        return info.CurrentAuthority != session.OwnControllerId;
    }

    /// <inheritdoc/>
    public bool? IsHeroAgentLocallyOwned(Hero hero)
    {
        if (!agentRegistry.TryGetHeroAgentInfo(hero, out var info)) return null;

        return info.CurrentAuthority == session.OwnControllerId;
    }
}
