using Common.Messaging;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

internal readonly struct PlayerPartyTradeOfferChanged : IEvent
{
    public readonly string SessionId;
    public readonly InventoryLogic InventoryLogic;
    public readonly BarterVM BarterVM;

    public PlayerPartyTradeOfferChanged(string sessionId, InventoryLogic inventoryLogic)
    {
        SessionId = sessionId;
        InventoryLogic = inventoryLogic;
        BarterVM = null;
    }

    public PlayerPartyTradeOfferChanged(string sessionId, BarterVM barterVM)
    {
        SessionId = sessionId;
        InventoryLogic = null;
        BarterVM = barterVM;
    }
}
