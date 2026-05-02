using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.TroopRosters.Handlers;
internal class ServerTroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTroopRosterHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ServerTroopRosterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<TroopRosterAddToCountsChanged>(HandleAddToCounts);
        messageBroker.Subscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<ClientRequestRecruitment> payload)
    {
        var obj = payload.What;
        var message = new RecruitTroops(obj.MobilePartyId, obj.TroopsInCart);
        messageBroker.Publish(this, message);
    }

    private void HandleAddToCounts(MessagePayload<TroopRosterAddToCountsChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MobileParty, out var mobilePartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CharacterObject, out var characterObjectId)) return;

        var message = new NetworkChangeTroopRosterAddtoCounts(mobilePartyId, characterObjectId, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.xpChanged, obj.RemoveDepleted, obj.Index);
        network.SendAll(message);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<TroopRosterAddToCountsChanged>(HandleAddToCounts);
        messageBroker.Unsubscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }
}