using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

internal readonly struct EndPlayerCaptivityAttempted : IEvent
{
    public readonly Hero PlayerHero;
    public readonly EndCaptivityDetail Detail;
    public readonly Hero Facilitator;

    public EndPlayerCaptivityAttempted(Hero prisoner, EndCaptivityDetail detail, Hero facilitator)
    {
        PlayerHero = prisoner;
        Detail = detail;
        Facilitator = facilitator;
    }
}