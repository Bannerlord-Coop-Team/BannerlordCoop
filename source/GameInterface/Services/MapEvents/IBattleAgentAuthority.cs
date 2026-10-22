using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Answers which client owns a coop battle agent, for GameInterface patches that cannot see the Missions agent registry.
/// Implemented in Missions by the live battle and reached through <see cref="IBattleAgentAuthorityRegistry"/>.
/// </summary>
public interface IBattleAgentAuthority
{
    /// <summary>
    /// True when <paramref name="mount"/> is a registered horse owned by another client, false when this client owns it,
    /// null when it is not registered, so callers fall back to the rider's ownership.
    /// </summary>
    bool? IsMountRemotelyOwned(Agent mount);

    /// <summary>
    /// True when this client owns <paramref name="hero"/>'s battle agent, false when another client does,
    /// null when the hero has no registered agent.
    /// </summary>
    bool? IsHeroAgentLocallyOwned(Hero hero);
}
