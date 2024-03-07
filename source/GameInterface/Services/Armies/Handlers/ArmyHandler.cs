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
        messageBroker.Subscribe<RemovePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
    }


    private void HandleChangeRemoveMobilePartyInArmy(MessagePayload<RemovePartyInArmy> payload)
    {
        var data = payload.What.Data;

        if (objectManager.TryGetObject(data.PartyStringId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", data.PartyStringId);
            return;
        }

        if (objectManager.TryGetNonMBObject<Army>(data.ArmyStringId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", data.ArmyStringId);
            return;
        }

        ArmyPatches.RemoveMobilePartyInArmy(mobileParty, army);

    }

    //Generate Handler Methods
    private void HandleChangeAddMobilePartyInArmy(MessagePayload<AddMobilePartyInArmy> payload)
    {
        var obj = payload.What.Data;

        if (objectManager.TryGetObject(obj.PartyStringId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.PartyStringId);
            return;
        }

        if (objectManager.TryGetNonMBObject<Army>(obj.ArmyStringId, out var army) == false)
        {
            Logger.Error("Unable to find Army ({armyId})", obj.ArmyStringId);
            return;
        }

        ArmyPatches.AddMobilePartyInArmy(mobileParty, army);
          
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        messageBroker.Unsubscribe<RemovePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
    }
}
