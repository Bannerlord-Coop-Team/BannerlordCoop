using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>Published on the server game thread after a player party's captivity state is restored.</summary>
public readonly struct PlayerPartyReleasedFromCaptivity : IEvent
{
    public readonly MobileParty PlayerParty;

    public PlayerPartyReleasedFromCaptivity(MobileParty playerParty)
    {
        PlayerParty = playerParty;
    }
}
