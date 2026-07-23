using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Client-local request to change the <see cref="Hero.HitPoints"/> of the hero this client controls — e.g.
/// the hero was wounded in the client's own mission (<c>Mission.OnAgentRemoved</c> → <c>set_HitPoints</c>).
/// <c>Hero.HitPoints</c> is server-authoritative, so this change is forwarded to the server rather than kept
/// local-only (which would desync the hero's health/wounded state).
/// </summary>
internal readonly struct HeroHitPointsChangeRequested : IEvent
{
    public readonly Hero Hero;
    public readonly int HitPoints;

    public HeroHitPointsChangeRequested(Hero hero, int hitPoints)
    {
        Hero = hero;
        HitPoints = hitPoints;
    }
}
