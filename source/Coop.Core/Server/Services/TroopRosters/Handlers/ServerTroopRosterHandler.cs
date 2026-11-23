using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;

namespace Coop.Core.Server.Services.TroopRosters.Handlers;
internal class ServerTroopRosterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ITroopRosterInterface troopRosterInterface;

    public ServerTroopRosterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, IPlayerManager playerManager, ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<ClientRequestRecruitment> payload)
    {
        var data = payload.What;
        var peer = payload.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            // A stale or wrong party id must not recruit into, or charge, another player's party.
            if (!playerManager.IsSenderControllingParty(payload.Who, data.MobilePartyId)) return;

            int skipped = troopRosterInterface.HandleOnRecruitmentDone(data.MobilePartyId, data.TroopsInCart);

            // The client closed its recruit screen on Done, so tell it why its party did not get the whole cart
            if (skipped > 0 && peer != null)
                network.Send(peer, new SendInformationMessage($"{skipped} troop(s) could not be recruited because the volunteers or your gold changed. Open the recruit screen again to see what is left."));
        });
    }
}