using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Handlers;

/// <summary>
/// Handler for <see cref="Army"/> messages
/// </summary>
public class ArmyHandler : IHandler
{
    
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();
    private ArmyQueueManager queueManager = new ArmyQueueManager();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public ArmyHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Subscribe<RemoveMobilePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
        messageBroker.Subscribe<DestroyArmy>(HandleChangeDisbandArmy);
        messageBroker.Subscribe<CreateArmy>(HandleChangeCreateArmy);
    }


    private void HandleChangeDisbandArmy(MessagePayload<DestroyArmy> payload)
    {
        var data = payload.What.Data;

        if (objectManager.TryGetObject<Army>(data.ArmyId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", data.ArmyId);
            return;
        }
        Army.ArmyDispersionReason armyReason = (Army.ArmyDispersionReason)data.Reason;
        ArmyDeletionPatch.DisbandArmy(army, armyReason);
    }

    private void HandleChangeCreateArmy(MessagePayload<CreateArmy> payload)
    {
        var data = payload.What.Data;

        if (objectManager.TryGetObject<Kingdom>(data.KingdomStringId, out var kingdom) == false)
        {
            Logger.Error("Unable to find Kingdom ({kingdomId})", data.KingdomStringId);
            return;
        }

        if (objectManager.TryGetObject<Hero>(data.LeaderHeroStringId, out var armyLeader) == false)
        {
            Logger.Error("Unable to find MobileParty ({armyLeaderId})", data.LeaderHeroStringId);
            return;
        }

        if (objectManager.TryGetObject<Settlement>(data.TargetSettlementStringId, out var targetSettlement) == false)
        {
            Logger.Error("Unable to find Settlement ({targetSettlement})", data.TargetSettlementStringId);
            return;
        }

        Army.ArmyTypes armyType = (Army.ArmyTypes)data.SelectedArmyType;

        ArmyCreationPatch.CreateArmyInKingdom(kingdom, armyLeader, targetSettlement, armyType, data.ArmyStringId);
    }


    private void HandleChangeRemoveMobilePartyInArmy(MessagePayload<RemoveMobilePartyInArmy> payload)
    {
        var obj = payload.What;

        List<MobileParty> mobilePartyList = new List<MobileParty>();
        foreach (var mobilePartyId in obj.MobilePartyIds)
        {
            if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Unable to find MobileParty ({mobilePartyId})", mobilePartyId);
                return;
            }

            mobilePartyList.Add(mobileParty);
        }

        if (objectManager.TryGetObject<Army>(obj.ArmyId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", obj.ArmyId);
            return;
        }
        ArmyPatches.SetMobilePartyListInArmy(mobilePartyList, army);

    }

    //Generate Handler Methods
    private void HandleChangeAddMobilePartyInArmy(MessagePayload<AddMobilePartyInArmy> payload)
    {
        var obj = payload.What;
        List<MobileParty> mobilePartyList = new List<MobileParty>();
        foreach(var mobilePartyId in obj.MobilePartyListId)
        {
            if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Unable to find MobileParty ({mobilePartyId})", mobilePartyId);
                return;
            }

            mobilePartyList.Add(mobileParty);
        }

        if (objectManager.TryGetObject<Army>(obj.ArmyId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", obj.ArmyId);
            
            // We add the mobile party to the queue to be processed later when the army is created
            queueManager.Enqueue(obj.ArmyId, obj.MobilePartyId);
            return;
        }
        ArmyPatches.SetMobilePartyListInArmy(mobilePartyList, army);
          
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Unsubscribe<RemoveMobilePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
        messageBroker.Unsubscribe<DestroyArmy>(HandleChangeDisbandArmy);
        messageBroker.Unsubscribe<CreateArmy>(HandleChangeCreateArmy);
    }
}
