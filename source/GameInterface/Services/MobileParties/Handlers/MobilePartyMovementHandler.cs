using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Movement;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class MobilePartyMovementHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public MobilePartyMovementHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<UpdateTargetPosition>(Handle_UpdateTargetPosition);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateTargetPosition>(Handle_UpdateTargetPosition);
    }

    private bool TryGetMobileParty(string partyId, out MobileParty mobileParty)
    {
        if (objectManager.TryGetObject(partyId, out mobileParty) == false)
        {
            Logger.Error("Unable to find party with id: {partyId}", partyId);
            return false;
        }

        return true;
    }

    private void Handle_UpdateTargetPosition(MessagePayload<UpdateTargetPosition> obj)
    {
        var partyId = obj.What.PartyId;

        if (TryGetMobileParty(partyId, out var mobileParty) == false) return;

        PartyMovementPatch.SetTargetPosition(mobileParty, obj.What.TargetPosition);
    }
}
