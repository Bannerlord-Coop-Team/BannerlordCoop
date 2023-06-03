using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Handlers;

internal class MobilePartyMovementHandler : IHandler
{
    private readonly ILogger Logger = LogManager.GetLogger<MobilePartyMovementHandler>();
    private readonly IObjectManager objectManager;
    private readonly IControlledEntityRegistery controlledEntityRegistry;
    private readonly IMessageBroker messageBroker;

    public MobilePartyMovementHandler(
        IObjectManager objectManager,
        IControlledEntityRegistery controlledEntityRegistry,
        IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<UpdatePartyTargetPosition>(Handle_UpdatePartyTargetPosition);
        messageBroker.Subscribe<PartyTargetPositionAttempted>(Handle_PartyTargetPositionChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdatePartyTargetPosition>(Handle_UpdatePartyTargetPosition);
        messageBroker.Unsubscribe<PartyTargetPositionAttempted>(Handle_PartyTargetPositionChanged);
    }

    private void Handle_PartyTargetPositionChanged(MessagePayload<PartyTargetPositionAttempted> obj)
    {
        var payload = obj.What;

        if(objectManager.TryGetId(payload.Party, out string partyId))
        {
            if (controlledEntityRegistry.IsOwned(partyId))
            {
                var message = new ControlledPartyTargetPositionUpdated(partyId, payload.NewTargetPosition);
                messageBroker.Publish(this, message);
            }
        }
        else
        {
            Logger.Error("Unable to find HeroId for {party}", payload.Party.Name);
        }
    }

    private void Handle_UpdatePartyTargetPosition(MessagePayload<UpdatePartyTargetPosition> obj)
    {
        var targetPositionData = obj.What.TargetPositionData;

        if (objectManager.TryGetObject(targetPositionData.PartyId, out MobileParty resolvedParty) == false)
        {
            Logger.Error("Unable to find hero for {guid}", targetPositionData.PartyId);
            return;
        }

        Vec2 targetPos = targetPositionData.TargetPosition;
        Logger.Debug($"Setting {resolvedParty.StringId} to {targetPos}");

        PartyMovementPatch.SetTargetPositionOriginal(resolvedParty, ref targetPos);
    }
}
