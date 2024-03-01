using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Messages;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;

namespace Coop.Core.Server.Services.TroopRosters.Handlers;
internal class ServerTroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTroopRosterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerTroopRosterHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<TroopRosterAddToCountsChanged>(HandleAddToCounts);
        messageBroker.Subscribe<ClientRequestOnDoneRecruitmentVM>(HandleOnRecruitmentDone);


    }

    private void HandleOnRecruitmentDone(MessagePayload<ClientRequestOnDoneRecruitmentVM> payload)
    {
        var obj = payload.What;
        var message = new ProccessRequestOnDoneRecruitmentVM(obj.MobilePartyId, obj.TroopsInCart);
        messageBroker.Publish(this, message);
    }

    private void HandleAddToCounts(MessagePayload<TroopRosterAddToCountsChanged> payload)
    {
        var obj = payload.What;
        var message = new NetworkChangeTroopRosterAddtoCounts(obj.MobilePartyId, obj.Character, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.xpChanged, obj.RemoveDepleted, obj.Index);
        network.SendAll(message);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TroopRosterAddToCountsChanged>(HandleAddToCounts);
        messageBroker.Unsubscribe<ClientRequestOnDoneRecruitmentVM>(HandleOnRecruitmentDone);


    }
}
