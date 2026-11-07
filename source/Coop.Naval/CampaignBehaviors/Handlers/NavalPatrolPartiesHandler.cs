using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.CampaignBehaviors.Interfaces;
using Coop.Naval.CampaignBehaviors.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Handlers;

internal class NavalPatrolPartiesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly INavalPatrolPartiesCampaignBehaviorInterface navalPatrolPartiesInterface;

    public NavalPatrolPartiesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        INavalPatrolPartiesCampaignBehaviorInterface navalPatrolPartiesInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.navalPatrolPartiesInterface = navalPatrolPartiesInterface;

        messageBroker.Subscribe<NavalPatrolSettlementChanged>(Handle_NavalPatrolSettlementChanged);
        messageBroker.Subscribe<NetworkNavalPatrolSettlementChanged>(Handle_NetworkNavalPatrolSettlementChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NavalPatrolSettlementChanged>(Handle_NavalPatrolSettlementChanged);
        messageBroker.Unsubscribe<NetworkNavalPatrolSettlementChanged>(Handle_NetworkNavalPatrolSettlementChanged);
    }

    private void Handle_NavalPatrolSettlementChanged(MessagePayload<NavalPatrolSettlementChanged> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetHandleWithLogging(data.Settlement, out var settlementHandle)) return;

        uint patrolPartyHandle = 0;
        if (data.PatrolParty != null && !objectManager.TryGetHandleWithLogging(data.PatrolParty, out patrolPartyHandle)) return;

        network.SendAll(new NetworkNavalPatrolSettlementChanged(settlementHandle, data.IsQueued, data.QueueTime, patrolPartyHandle));
    }

    private void Handle_NetworkNavalPatrolSettlementChanged(MessagePayload<NetworkNavalPatrolSettlementChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementHandle, out var settlement)) return;

            MobileParty patrolParty = null;
            if (data.PatrolPartyHandle != 0 && !objectManager.TryGetObjectWithLogging(data.PatrolPartyHandle, out patrolParty)) return;

            navalPatrolPartiesInterface.SetSettlementState(settlement, data.IsQueued, data.QueueTime, patrolParty);
        });
    }
}
