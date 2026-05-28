using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Inventory.Handlers;

internal class TransferHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<TransferHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public TransferHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<TransferAttempted>(Handle_TransferAttempted);
        messageBroker.Subscribe<CompleteTransfer>(Handle_CompleteTransfer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TransferAttempted>(Handle_TransferAttempted);
        messageBroker.Unsubscribe<CompleteTransfer>(Handle_CompleteTransfer);
    }

    private void Handle_TransferAttempted(MessagePayload<TransferAttempted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.TargetItemRoster, out var targetItemRosterId)) return;

        var message = new CompleteTransfer(
            targetItemRosterId,
            obj.What.EquipmentElement,
            obj.What.Count);

        network.SendAll(message);
    }

    private void Handle_CompleteTransfer(MessagePayload<CompleteTransfer> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.TargetItemRosterId, out var targetItemRoster)) return;

        targetItemRoster.AddToCounts(obj.What.EquipmentElement, obj.What.Count);

        network.SendAll(new RefreshOtherInventory(obj.What.TargetItemRosterId));
    }
}
