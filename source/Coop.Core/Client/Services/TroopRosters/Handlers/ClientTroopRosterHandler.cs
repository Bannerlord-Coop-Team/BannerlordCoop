using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.GameDebug.Messages;
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

        messageBroker.Subscribe<RecruitmentAttempted>(HandleOnRecruitmentDone);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RecruitmentAttempted>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<RecruitmentAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MobileParty, out var mobilePartyId))
        {
            AbortCart("Recruitment could not be sent because your party is still synchronizing. Please reopen the recruitment screen.");
            return;
        }

        List<TroopInfo> troops = new();
        foreach (var (hero, character, index) in obj.TroopsInCart)
        {
            if (!objectManager.TryGetIdWithLogging(hero, out var heroId) ||
                !objectManager.TryGetIdWithLogging(character, out var characterId))
            {
                AbortCart("Recruitment changed while the screen was open. No troops were recruited; please reopen the recruitment screen.");
                return;
            }

            troops.Add(new TroopInfo(heroId, characterId, index));
        }

        if (troops.Count <= 0)
        {
            Logger.Warning("No troops in cart");
            return;
        }

        var message = new ClientRequestRecruitment(mobilePartyId, troops.ToArray());

        network.SendAll(message);
    }

    private void AbortCart(string reason)
    {
        Logger.Warning(reason);
        messageBroker.Publish(this, new SendInformationMessage(reason));
    }
}
