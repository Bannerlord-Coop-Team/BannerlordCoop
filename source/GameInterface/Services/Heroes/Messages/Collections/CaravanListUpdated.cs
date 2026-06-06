using Common.Messaging;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record CaravanListUpdated : GenericEvent<Hero, CaravanPartyComponent>
{
    public CaravanListUpdated(Hero instance, CaravanPartyComponent value) : base(instance, value)
    {
    }
}
