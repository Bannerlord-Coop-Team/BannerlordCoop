using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Handlers;

internal class ResetRostersHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<ResetRostersHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ResetRostersHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ResetRosters>(Handle_ResetRosters);
        messageBroker.Subscribe<CompleteResetRosters>(Handle_CompleteResetRosters);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ResetRosters>(Handle_ResetRosters);
        messageBroker.Unsubscribe<CompleteResetRosters>(Handle_CompleteResetRosters);
    }

    private void Handle_ResetRosters(MessagePayload<ResetRosters> obj)
    {
        string targetItemRoster1Id = null;
        if (obj.What.TargetItemRoster1 != null && !objectManager.TryGetIdWithLogging(obj.What.TargetItemRoster1, out targetItemRoster1Id)) return;

        string targetItemRoster2Id = null;
        if (obj.What.TargetItemRoster2 != null && !objectManager.TryGetIdWithLogging(obj.What.TargetItemRoster2, out targetItemRoster2Id)) return;

        var backupItemRoster1Elements = obj.What.BackupItemRoster1._data;
        var backupItemRoster2Elements = obj.What.BackupItemRoster2._data;

        var message = new CompleteResetRosters(
            targetItemRoster1Id,
            targetItemRoster2Id,
            backupItemRoster1Elements,
            backupItemRoster2Elements);

        network.SendAll(message);
    }

    private void Handle_CompleteResetRosters(MessagePayload<CompleteResetRosters> obj)
    {
        ItemRoster targetItemRoster1 = null;
        if (obj.What.TargetItemRoster1Id != null && !objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.TargetItemRoster1Id, out targetItemRoster1)) return;

        ItemRoster targetItemRoster2 = null;
        if (obj.What.TargetItemRoster2Id != null && !objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.TargetItemRoster2Id, out targetItemRoster2)) return;

        targetItemRoster1?.Clear();
        foreach(var itemRosterElement in obj.What.BackupItemRoster1Elements ?? Enumerable.Empty<ItemRosterElement>())
        {
            targetItemRoster1?.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
        }

        targetItemRoster2?.Clear();
        foreach (var itemRosterElement in obj.What.BackupItemRoster2Elements ?? Enumerable.Empty<ItemRosterElement>())
        {
            targetItemRoster2?.AddToCounts(itemRosterElement.EquipmentElement, itemRosterElement.Amount);
        }

        //network.SendAll(new RefreshOtherInventory(obj.What.TargetItemRoster1Id));
    }
}
