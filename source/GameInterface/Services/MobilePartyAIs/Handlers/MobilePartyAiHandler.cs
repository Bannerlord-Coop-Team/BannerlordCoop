using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobilePartyAIs.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using Serilog.Core;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Handlers;

internal class MobilePartyAiHandler : IHandler
{
    private readonly ILogger logger = LogManager.GetLogger<MobilePartyAiHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MobilePartyAiHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<AiBehaviorInteractablePointUpdated>(Handle_AiBehaviorInteractablePointUpdated);
        messageBroker.Subscribe<UpdateAiBehaviorInteractablePoint>(Handle_UpdateAiBehaviorInteractablePoint);
        
    }

    public void Dispose()
    {
    }

    private void Handle_AiBehaviorInteractablePointUpdated(MessagePayload<AiBehaviorInteractablePointUpdated> payload)
    {
        var interactablePoint = payload.What.InteractablePoint;

        if (!objectManager.TryGetId(payload.What.PartyAi, out var partyAiId))
        {
            return;
        }

        if (interactablePoint is null)
        {
            network.SendAll(new UpdateAiBehaviorInteractablePoint(partyAiId, null, true));
            return;
        }

        if (interactablePoint is not PartyBase partyBase)
        {
            logger.Error("{type} is not handled", interactablePoint.GetType());
            return;
        }


        if (!objectManager.TryGetId(partyBase, out var interactablePointId))
        {
            return;
        }

        network.SendAll(new UpdateAiBehaviorInteractablePoint(partyAiId, interactablePointId));
    }

    private void Handle_UpdateAiBehaviorInteractablePoint(MessagePayload<UpdateAiBehaviorInteractablePoint> payload)
    {
        if (!objectManager.TryGetObject<MobilePartyAi>(payload.What.MobilePartyAiId, out var partyAi))
        {
            return;
        }

        if (payload.What.IsNull)
        {
            using (new AllowedThread())
            {
                partyAi.AiBehaviorInteractable = null;
                return;
            }
        }

        if (!objectManager.TryGetObject<PartyBase>(payload.What.InteractablePointId, out var interactablePoint))
        {
            return;
        }

        using (new AllowedThread())
        {
            partyAi.AiBehaviorInteractable = interactablePoint;
        }
    }
}
