using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Messages;
using GameInterface.Services.TroopRosters.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.TroopRosters.Handlers;
public class ClientTroopRosterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientTroopRosterHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkChangeTroopRosterAddtoCounts>(HandleAddToCounts);
        messageBroker.Subscribe<OnDoneRecruitmentVMChanged>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<OnDoneRecruitmentVMChanged> payload)
    {
        var obj = payload.What;
        var message = new ClientRequestOnDoneRecruitmentVM(obj.MobilePartyId, obj.TroopsInCart);

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
