using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyCaravanTransaction : IEvent
{
    public readonly MobileParty CaravanParty;
    public readonly Town Town;
    public readonly List<(EquipmentElement, int)> Items;

    public NotifyCaravanTransaction(MobileParty caravanParty, Town town, List<(EquipmentElement, int)> items)
    {
        CaravanParty = caravanParty;
        Town = town;
        Items = items;
    }
}
