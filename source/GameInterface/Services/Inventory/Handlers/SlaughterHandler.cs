using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Inventory.Handlers;

internal class SlaughterHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<SlaughterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public SlaughterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ItemSlaughtered>(Handle_ItemSlaughtered);
        messageBroker.Subscribe<SlaughterItem>(Handle_SlaughterItem);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemSlaughtered>(Handle_ItemSlaughtered);
        messageBroker.Unsubscribe<SlaughterItem>(Handle_SlaughterItem);
    }

    private void Handle_ItemSlaughtered(MessagePayload<ItemSlaughtered> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.TargetItemRoster, out var targetItemRosterId)) return;

        var message = new SlaughterItem(
            targetItemRosterId,
            obj.What.EquipmentElement,
            obj.What.MeatCount,
            obj.What.HideCount);

        network.SendAll(message);
    }

    private void Handle_SlaughterItem(MessagePayload<SlaughterItem> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.TargetItemRosterId, out var targetItemRoster)) return;

        targetItemRoster.AddToCounts(DefaultItems.Meat, obj.What.MeatCount);
        targetItemRoster.AddToCounts(obj.What.EquipmentElement, -1);
        targetItemRoster.AddToCounts(DefaultItems.Hides, obj.What.HideCount);
    }
}
