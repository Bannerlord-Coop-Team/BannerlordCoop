using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record WarPartyComponentsCacheUpdated : GenericEvent<Kingdom, WarPartyComponent>
{
    public WarPartyComponentsCacheUpdated(Kingdom instance, WarPartyComponent value) : base(instance, value)
    {
    }
}
