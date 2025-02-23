using Common.Messaging;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Heroes.Messages.Collections;

internal record CaravanListRemoved : GenericListEvent<Hero, CaravanPartyComponent>
{
    public CaravanListRemoved(Hero instance, CaravanPartyComponent value) : base(instance, value)
    {
    }
}
