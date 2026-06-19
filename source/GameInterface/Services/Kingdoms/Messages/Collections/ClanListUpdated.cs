using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record ClanListUpdated : GenericEvent<Kingdom, Clan>
{
    public ClanListUpdated(Kingdom instance, Clan value) : base(instance, value)
    {
    }
}
