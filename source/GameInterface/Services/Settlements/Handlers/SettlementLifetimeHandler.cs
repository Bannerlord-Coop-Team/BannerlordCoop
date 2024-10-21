using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers;
internal class SettlementLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public SettlementLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<SettlementCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateSettlement>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SettlementCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateSettlement>(Handle);
    }


    private void Handle(MessagePayload<SettlementCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateSettlement(id));
    }

    private void Handle(MessagePayload<NetworkCreateSettlement> payload)
    {
        var newSettlement = ObjectHelper.SkipConstructor<Settlement>();

        objectManager.AddExisting(payload.What.SettlementId, newSettlement);

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                newSettlement.InitSettlement();
            }
        });
    }
}
