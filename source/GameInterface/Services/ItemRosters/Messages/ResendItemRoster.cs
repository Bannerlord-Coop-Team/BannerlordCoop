using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters.Messages;

public readonly struct ResendItemRoster : ICommand
{
    public readonly ItemRoster ItemRoster;

    public ResendItemRoster(ItemRoster itemRoster)
    {
        ItemRoster = itemRoster;
    }
}
