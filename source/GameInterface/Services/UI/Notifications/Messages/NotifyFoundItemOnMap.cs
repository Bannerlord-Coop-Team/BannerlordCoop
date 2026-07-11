using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyFoundItemOnMap : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly int Count;
    public readonly TextObject ItemName;

    public NotifyFoundItemOnMap(MobileParty mobileParty, int count, TextObject itemName)
    {
        MobileParty = mobileParty;
        Count = count;
        ItemName = itemName;
    }
}
