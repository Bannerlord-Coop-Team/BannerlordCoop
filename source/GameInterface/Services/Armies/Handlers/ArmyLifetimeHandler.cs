using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.Army;

namespace GameInterface.Services.Armies.Handlers;

/// <summary>
/// Handler for <see cref="Army"/> messages
/// </summary>
public class ArmyLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ArmyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<ArmyCreated>(Handle_ArmyCreated);
        messageBroker.Subscribe<NetworkCreateArmy>(Handle_CreateArmy);

        messageBroker.Subscribe<ArmyDestroyed>(Handle_ArmyDestroyed);
        messageBroker.Subscribe<NetworkDestroyArmy>(Handle_DestroyArmy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArmyCreated>(Handle_ArmyCreated);
        messageBroker.Unsubscribe<NetworkCreateArmy>(Handle_CreateArmy);

        messageBroker.Unsubscribe<ArmyDestroyed>(Handle_ArmyDestroyed);
        messageBroker.Unsubscribe<NetworkDestroyArmy>(Handle_DestroyArmy);
    }

    private void Handle_ArmyCreated(MessagePayload<ArmyCreated> payload)
    {
        var army = payload.What.Army;
        var kingom = payload.What.Kingdom;
        var mobileParty = payload.What.MobileParty;
        var type = (short)payload.What.ArmyType;

        if (objectManager.TryGetId(kingom, out var kingdomId) == false) return;
        if (objectManager.TryGetId(mobileParty, out var mobilePartyId) == false) return;

        objectManager.AddNewObject(army, out var armyId);

        var data = new ArmyCreationData(armyId, kingdomId, mobilePartyId, type);
        var message = new NetworkCreateArmy(data);
        network.SendAll(message);
    }

    private void Handle_CreateArmy(MessagePayload<NetworkCreateArmy> payload)
    {
        var data = payload.What.Data;
        var armyId = data.StringId;
        var kingdomId = data.KingdomId;
        var leaderPartyId = data.LeaderPartyId;
        var armyType = (ArmyTypes)data.ArmyType;

        
        if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false) return;
        if (objectManager.TryGetObject(leaderPartyId, out MobileParty leaderParty) == false) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                var army = new Army(kingdom, leaderParty, armyType);
                objectManager.AddExisting(armyId, army);
            }
        });
    }

    private void Handle_ArmyDestroyed(MessagePayload<ArmyDestroyed> payload)
    {
        var army = payload.What.Army;
        if (objectManager.Remove(army) == false)
        {
            Logger.Error("Could not remove Army {army} from {name}", army.Name, nameof(IObjectManager));
            return;
        }

        var message = new NetworkDestroyArmy(payload.What.Data);
        network.SendAll(message);
    }

    private void Handle_DestroyArmy(MessagePayload<NetworkDestroyArmy> payload)
    {
        var stringId = payload.What.Data.StringId;
        var reason = (ArmyDispersionReason)payload.What.Data.Reason;

        if(objectManager.TryGetObject(stringId, out Army army) == false)
        {
            Logger.Error("Failed to find army with stringId {stringId}", stringId);
            return;
        }

        if (objectManager.Remove(army) == false)
        {
            Logger.Error("Failed to remove army with stringId {stringId}", stringId);
            return;
        }

        ArmyLifetimePatches.OverrideDestroyArmy(army, reason);
    }
}
