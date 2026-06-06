using Common.Messaging;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record CaravanListRemoved : GenericEvent<Hero, CaravanPartyComponent>
{
    public CaravanListRemoved(Hero instance, CaravanPartyComponent value) : base(instance, value)
    {
    }
}
