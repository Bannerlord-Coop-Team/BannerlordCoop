using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

internal readonly struct RequestMapEventPartyUpdate : ICommand
{
    public readonly MapEventParty MapEventParty;

    public RequestMapEventPartyUpdate(MapEventParty mapEventParty)
    {
        MapEventParty = mapEventParty;
    }
}
