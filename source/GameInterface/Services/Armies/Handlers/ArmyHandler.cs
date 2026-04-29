using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Handlers;

/// <summary>
/// Handler for <see cref="Army"/> messages
/// </summary>
public class ArmyHandler : IHandler
{
    
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ArmyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        messageBroker.Subscribe<NetworkAddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Subscribe<MobilePartyInArmyRemoved>(HandleRemoveMobilePartyInArmy);
        messageBroker.Subscribe<NetworkRemovePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        messageBroker.Unsubscribe<NetworkAddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Unsubscribe<MobilePartyInArmyRemoved>(HandleRemoveMobilePartyInArmy);
        messageBroker.Unsubscribe<NetworkRemovePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
    }

    private void HandleAddMobilePartyInArmy(MessagePayload<MobilePartyInArmyAdded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new NetworkAddMobilePartyInArmy(armyId, mobilePartyId);

        // Broadcast to all the clients that the state was changed   
        network.SendAll(message);
    }

    private void HandleChangeAddMobilePartyInArmy(MessagePayload<NetworkAddMobilePartyInArmy> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObjectWithLogging(obj.MobilePartyId, out MobileParty mobileParty) == false) return;
        if (objectManager.TryGetObjectWithLogging<Army>(obj.ArmyId, out var army) == false) return;

        ArmyPatches.AddMobilePartyInArmy(mobileParty, army);
    }

    private void HandleRemoveMobilePartyInArmy(MessagePayload<MobilePartyInArmyRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Army, out var armyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var message = new NetworkRemovePartyInArmy(armyId, mobilePartyId);

        // Broadcast to all the clients that the state was changed
        network.SendAll(message);
    }

    private void HandleChangeRemoveMobilePartyInArmy(MessagePayload<NetworkRemovePartyInArmy> payload)
    {
        var data = payload.What;

        if (objectManager.TryGetObjectWithLogging(data.MobilePartyId, out MobileParty mobileParty) == false) return;
        if (objectManager.TryGetObjectWithLogging<Army>(data.ArmyId, out var army) == false) return;

        ArmyPatches.RemoveMobilePartyInArmy(mobileParty, army);
    }
}
