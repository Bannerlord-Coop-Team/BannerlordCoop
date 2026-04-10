using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;

namespace Coop.Core.Client.Services.TroopRosters.Handlers;
public class ClientTroopRosterHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<ClientTroopRosterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ClientTroopRosterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkChangeTroopRosterAddtoCounts>(HandleAddToCounts);
        messageBroker.Subscribe<OnDoneRecruitmentVMChanged>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<OnDoneRecruitmentVMChanged> payload)
    {
        var obj = payload.What;
        var message = new ClientRequestOnDoneRecruitmentVM(obj.MobilePartyId, obj.TroopsInCart, obj.TotalCost);

        network.SendAll(message);
    }
    private void HandleAddToCounts(MessagePayload<NetworkChangeTroopRosterAddtoCounts> payload)
    {
        var obj = payload.What;
        var message = new ChangeTroopRostersAddToCounts(obj.MobilePartyId, obj.Character, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.xpChanged, obj.RemoveDepleted, obj.Index);

        messageBroker.Publish(this, message);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeTroopRosterAddtoCounts>(HandleAddToCounts);
        messageBroker.Unsubscribe<OnDoneRecruitmentVMChanged>(HandleOnRecruitmentDone);
    }
}