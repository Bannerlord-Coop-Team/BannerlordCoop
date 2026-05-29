using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Serilog;
using System;

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
        messageBroker.Subscribe<TroopRosterAddToCountsChanged>(Handle_AddToCounts);
        messageBroker.Subscribe<TroopRosterAddToCountsAtIndexChanged>(Handle_AddToCountsAtIndex);
        messageBroker.Subscribe<TroopRosterAddHeroToCountsChanged>(Handle_HeroAddToCounts);
        messageBroker.Subscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TroopRosterAddToCountsChanged>(Handle_AddToCounts);
        messageBroker.Unsubscribe<TroopRosterAddHeroToCountsChanged>(Handle_HeroAddToCounts);
        messageBroker.Unsubscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<ClientRequestRecruitment> payload)
    {
        var obj = payload.What;
        var message = new RecruitTroops(obj.MobilePartyId, obj.TroopsInCart);
        messageBroker.Publish(this, message);
    }

    private void Handle_AddToCountsAtIndex(MessagePayload<TroopRosterAddToCountsAtIndexChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.TroopRoster, out var troopRosterId)) return;

        if (TroopRosterConfig.Debug)
        {
            Logger.Debug("[Server] Sending troop roster add to counts at index change for " +
                "TroopRoster {TroopRosterId}, " +
                "CharacterObject {CharacterObjectId}, " +
                "Count {Count}, " +
                "InsertAtFront {InsertAtFront}, " +
                "WoundedCount {WoundedCount}, " +
                "XpChanged {XpChanged}, " +
                "RemoveDepleted {RemoveDepleted}, " +
                "Index {Index}",
                troopRosterId,
                obj.Count,
                obj.WoundedCount,
                obj.XpChanged,
                obj.RemoveDepleted,
                obj.Index);
        }

        var message = new NetworkChangeTroopRosterAddtoCountsAtIndex(troopRosterId, obj.Index, obj.Count, obj.WoundedCount, obj.XpChanged, obj.RemoveDepleted);
        network.SendAll(message);
    }

    private void Handle_AddToCounts(MessagePayload<TroopRosterAddToCountsChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.TroopRoster, out var troopRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.CharacterObject, out var characterObjectId)) return;

        if (TroopRosterConfig.Debug)
        {
            Logger.Debug("[Server] Sending troop roster add to counts change for " +
                "TroopRoster {TroopRosterId}, " +
                "CharacterObject {CharacterObjectId}, " +
                "Count {Count}, " +
                "InsertAtFront {InsertAtFront}, " +
                "WoundedCount {WoundedCount}, " +
                "XpChanged {XpChanged}, " +
                "RemoveDepleted {RemoveDepleted}, " +
                "Index {Index}",
                troopRosterId, 
                characterObjectId, 
                obj.Count, 
                obj.InsertAtFront, 
                obj.WoundedCount, 
                obj.XpChanged, 
                obj.RemoveDepleted, 
                obj.Index);
        }

        var message = new NetworkChangeTroopRosterAddtoCounts(troopRosterId, characterObjectId, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.XpChanged, obj.RemoveDepleted, obj.Index);
        network.SendAll(message);
    }

    private void Handle_HeroAddToCounts(MessagePayload<TroopRosterAddHeroToCountsChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.TroopRoster, out var troopRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Hero, out var heroId)) return;

        if (TroopRosterConfig.Debug)
        {
            Logger.Debug("[Server] Sending troop roster add to counts change for " +
                "TroopRoster {TroopRosterId}, " +
                "Hero {HeroId}, " +
                "Count {Count}, " +
                "InsertAtFront {InsertAtFront}, " +
                "WoundedCount {WoundedCount}, " +
                "XpChanged {XpChanged}, " +
                "RemoveDepleted {RemoveDepleted}, " +
                "Index {Index}",
                troopRosterId,
                heroId,
                obj.Count,
                obj.InsertAtFront,
                obj.WoundedCount,
                obj.XpChanged,
                obj.RemoveDepleted,
                obj.Index);
        }

        var message = new NetworkChangeTroopRosterHeroAddtoCounts(troopRosterId, heroId, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.XpChanged, obj.RemoveDepleted, obj.Index);
        network.SendAll(message);
    }
}