using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages.Collections;

internal record ClanListRemoved : GenericEvent<Kingdom, Clan>
{
    public ClanListRemoved(Kingdom instance, Clan value) : base(instance, value)
    {
    }
}
