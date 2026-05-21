using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System.Collections.Generic;

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
        messageBroker.Subscribe<RecruitmentAttempted>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<RecruitmentAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MobileParty, out var mobilePartyId)) return;

        List<TroopInfo> troops = new();
        foreach (var (hero, character, index) in obj.TroopsInCart)
        {
            if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) continue;
            if (!objectManager.TryGetIdWithLogging(character, out var characterId)) continue;

            troops.Add(new TroopInfo(heroId, characterId, index));
        }

        if (troops.Count <= 0)
        {
            Logger.Warning("No troops in card");
            return;
        }

        var message = new ClientRequestRecruitment(mobilePartyId, troops.ToArray());

        network.SendAll(message);
    }
    private void HandleAddToCounts(MessagePayload<NetworkChangeTroopRosterAddtoCounts> payload)
    {
        var obj = payload.What;
        var message = new ChangeTroopRostersAddToCounts(obj.TroopRosterId, obj.CharacterId, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.XpChanged, obj.RemoveDepleted, obj.Index);

        Logger.Debug("[Client] Setting troop roster counts for TroopRosterId: {TroopRosterId}, CharacterId: {CharacterId}", obj.TroopRosterId, obj.CharacterId);

        messageBroker.Publish(this, message);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeTroopRosterAddtoCounts>(HandleAddToCounts);
        messageBroker.Unsubscribe<RecruitmentAttempted>(HandleOnRecruitmentDone);
    }
}