using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

internal readonly struct PlayerCaptivityChanged : IEvent
{
    public readonly PartyBase CaptorParty;

    public PlayerCaptivityChanged(PartyBase captorParty)
    {
        CaptorParty = captorParty;
    }
}
