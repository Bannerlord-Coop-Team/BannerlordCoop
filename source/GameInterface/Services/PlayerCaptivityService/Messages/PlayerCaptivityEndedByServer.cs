using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>
/// Raised on the server when it decides to release a player (client) hero from captivity itself — i.e.
/// not in response to a client request. The main case is the captor party being defeated in battle,
/// where native <c>MapEvent.LootDefeatedPartyPrisoners</c> calls
/// <see cref="EndCaptivityAction.ApplyByReleasedAfterBattle"/>; AI ransoms and peace releases go through
/// the same <c>EndCaptivityAction.ApplyInternal</c> entry point. The handler performs the full coop
/// release (restoring the deactivated player party and clearing the synced captivity state) that native
/// only does for the host's main hero.
/// </summary>
internal readonly struct PlayerCaptivityEndedByServer : IEvent
{
    public readonly Hero PrisonerHero;
    public readonly EndCaptivityDetail Detail;
    public readonly Hero Facilitator;

    public PlayerCaptivityEndedByServer(Hero prisonerHero, EndCaptivityDetail detail, Hero facilitator)
    {
        PrisonerHero = prisonerHero;
        Detail = detail;
        Facilitator = facilitator;
    }
}
