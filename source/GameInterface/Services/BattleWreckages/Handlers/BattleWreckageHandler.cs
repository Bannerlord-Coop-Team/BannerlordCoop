using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Handlers;

internal class BattleWreckageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface;

    public BattleWreckageHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.battleWreckageCampaignBehaviorInterface = battleWreckageCampaignBehaviorInterface;

        messageBroker.Subscribe<BattleWreckageAdded>(Handle_BattleWreckageAdded);
        messageBroker.Subscribe<NetworkAddBattleWreckage>(Handle_NetworkAddBattleWreckage);
        messageBroker.Subscribe<BattleWreckageRemoved>(Handle_BattleWreckageRemoved);
        messageBroker.Subscribe<NetworkRemoveBattleWreckage>(Handle_NetworkRemoveBattleWreckage);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleWreckageAdded>(Handle_BattleWreckageAdded);
        messageBroker.Unsubscribe<NetworkAddBattleWreckage>(Handle_NetworkAddBattleWreckage);
        messageBroker.Unsubscribe<BattleWreckageRemoved>(Handle_BattleWreckageRemoved);
        messageBroker.Unsubscribe<NetworkRemoveBattleWreckage>(Handle_NetworkRemoveBattleWreckage);
    }

    private void Handle_BattleWreckageAdded(MessagePayload<BattleWreckageAdded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.BattleWreckage, out var battleWreckageId)) return;

        network.SendAll(new NetworkAddBattleWreckage(battleWreckageId));
    }

    private void Handle_NetworkAddBattleWreckage(MessagePayload<NetworkAddBattleWreckage> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;

            battleWreckageCampaignBehaviorInterface.AddWreckageToMap(battleWreckage);
        });
    }

    private void Handle_BattleWreckageRemoved(MessagePayload<BattleWreckageRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.BattleWreckage, out var battleWreckageId)) return;

        network.SendAll(new NetworkRemoveBattleWreckage(battleWreckageId));
    }

    private void Handle_NetworkRemoveBattleWreckage(MessagePayload<NetworkRemoveBattleWreckage> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;

            battleWreckageCampaignBehaviorInterface.RemoveWreckageFromMap(battleWreckage);
        });
    }
}
