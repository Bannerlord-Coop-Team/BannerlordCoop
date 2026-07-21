using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>
/// A client chose to liberate a prisoner through the post-battle conversation.
/// </summary>
internal readonly struct PrisonerLiberationAttempted : IEvent
{
    public readonly Hero Prisoner;

    public PrisonerLiberationAttempted(Hero prisoner)
    {
        Prisoner = prisoner;
    }
}
