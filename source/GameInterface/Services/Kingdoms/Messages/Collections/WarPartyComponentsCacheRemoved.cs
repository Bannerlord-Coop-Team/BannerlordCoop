using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record WarPartyComponentsCacheRemoved : GenericEvent<Kingdom, WarPartyComponent>
{
    public WarPartyComponentsCacheRemoved(Kingdom instance, WarPartyComponent value) : base(instance, value)
    {
    }
}
