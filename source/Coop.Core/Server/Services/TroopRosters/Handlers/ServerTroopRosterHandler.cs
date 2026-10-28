using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.TroopRosters.Handlers;
internal class ServerTroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTroopRosterHandler>();

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

        GameThread.RunSafe(() =>
        {
            if (!DoesSenderControlParty(payload.Who, data.MobilePartyId)) return;

            troopRosterInterface.HandleOnRecruitmentDone(data.MobilePartyId, data.TroopsInCart);
        });
    }

    // A stale or wrong party id must not recruit into, or charge, another player's party.
    private bool DoesSenderControlParty(object sender, string mobilePartyId)
    {
        if (sender is NetPeer peer &&
            playerManager.TryGetPlayer(peer, out var player) &&
            player.MobilePartyId == mobilePartyId)
            return true;

        Logger.Warning("Ignoring recruitment for party {PartyId} from a peer that does not control it", mobilePartyId);
        return false;
    }
}