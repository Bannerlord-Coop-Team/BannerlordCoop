using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.HeroDevelopers.Messages;

public readonly struct UpdateItemRosterVersionAfterPerkChange : IEvent
{
    public readonly ItemRoster ItemRoster;

    public UpdateItemRosterVersionAfterPerkChange(ItemRoster itemRoster)
    {
        ItemRoster = itemRoster;
    }
}
