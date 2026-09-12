using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>
/// A client-local action attempted to release a hero controlled by the server.
/// </summary>
internal readonly struct EndCaptivityAttempted : IEvent
{
    public readonly Hero Prisoner;
    public readonly EndCaptivityDetail Detail;
    public readonly Hero Facilitator;
    public readonly bool ShowNotification;

    public EndCaptivityAttempted(Hero prisoner, EndCaptivityDetail detail, Hero facilitator, bool showNotification)
    {
        Prisoner = prisoner;
        Detail = detail;
        Facilitator = facilitator;
        ShowNotification = showNotification;
    }
}
