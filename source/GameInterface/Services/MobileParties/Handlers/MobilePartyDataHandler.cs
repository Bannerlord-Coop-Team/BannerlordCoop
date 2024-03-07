using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handler for party related messages.
/// </summary>
internal class MobilePartyDataHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public MobilePartyDataHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangePartyArmy>(Handle_ChangePartyArmy);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangePartyArmy>(Handle_ChangePartyArmy);
    }

    private void Handle_ChangePartyArmy(MessagePayload<ChangePartyArmy> payload)
    {
        var partyId = payload.What.Data.PartyId;
        var armyId = payload.What.Data.ArmyId;

        if (objectManager.TryGetObject(partyId, out MobileParty party) == false)
        {
            Logger.Error("Failed to find party with stringId {stringId}", partyId);
            return;
        }

        if (objectManager.TryGetNonMBObject(armyId, out Army army) == false)
        {
            Logger.Error("Failed to find army with stringId {stringId}", armyId);
            return;
        }

        PartyArmyPatches.OverrideSetArmy(party, army);
    }
}
