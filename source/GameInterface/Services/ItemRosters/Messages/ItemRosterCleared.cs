using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters.Messages;

public readonly struct ItemRosterCleared : ICommand
{
    public readonly ItemRoster ItemRoster;

    public ItemRosterCleared(ItemRoster itemRoster)
    {
        ItemRoster = itemRoster;
    }
}