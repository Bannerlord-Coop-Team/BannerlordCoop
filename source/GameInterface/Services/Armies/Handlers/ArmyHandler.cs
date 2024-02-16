using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
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
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public ArmyHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Subscribe<RemoveMobilePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
    }


    private void HandleChangeRemoveMobilePartyInArmy(MessagePayload<RemoveMobilePartyInArmy> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        if (objectManager.TryGetObject<Army>(obj.ArmyId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", obj.ArmyId);
            return;
        }

        ArmyPatches.RemoveMobilePartyInArmy(mobileParty, army);

    }

    //Generate Handler Methods
    private void HandleChangeAddMobilePartyInArmy(MessagePayload<AddMobilePartyInArmy> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        if (objectManager.TryGetObject<Army>(obj.ArmyId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", obj.ArmyId);
            return;
        }

        ArmyPatches.AddMobilePartyInArmy(mobileParty, army);
          
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Unsubscribe<RemoveMobilePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
    }
}
