using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace Coop.Naval.Notifications.Messages;

public readonly struct UpdateItemRosterVersionAfterPerkChange : IEvent
{
    public readonly ItemRoster ItemRoster;

    public UpdateItemRosterVersionAfterPerkChange(ItemRoster itemRoster)
    {
        ItemRoster = itemRoster;
    }
}
