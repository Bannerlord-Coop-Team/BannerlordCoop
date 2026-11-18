using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages;
using GameInterface.Services.Villages.Messages;
using TaleWorlds.CampaignSystem.Settlements;
using Serilog;

namespace Coop.Core.Client.Services.Villages.Handlers;

internal class ClientVillageHostileActionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientVillageHostileActionHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly IClientVillageHostileActionTracker clientVillageHostileActionTracker;

    public ClientVillageHostileActionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider,
        IVillageHostileActionInterface villageHostileActionInterface,
        IClientVillageHostileActionTracker clientVillageHostileActionTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.clientVillageHostileActionTracker = clientVillageHostileActionTracker;

        messageBroker.Subscribe<VillageHostileActionAttempted>(Handle_VillageHostileActionAttempted);
        messageBroker.Subscribe<NetworkVillageHostileActionStarted>(Handle_NetworkVillageHostileActionStarted);
        messageBroker.Subscribe<NetworkPromptRaidContinuation>(Handle_NetworkPromptRaidContinuation);
        messageBroker.Subscribe<NetworkVillageHostileActionDenied>(Handle_NetworkVillageHostileActionDenied);
        messageBroker.Subscribe<NetworkVillageHostileActionCooldowns>(Handle_NetworkVillageHostileActionCooldowns);
        messageBroker.Subscribe<NetworkAuthorizeForceTransfer>(Handle_NetworkAuthorizeForceTransfer);
        messageBroker.Subscribe<NetworkDenyForceTransfer>(Handle_NetworkDenyForceTransfer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageHostileActionAttempted>(Handle_VillageHostileActionAttempted);
        messageBroker.Unsubscribe<NetworkVillageHostileActionStarted>(Handle_NetworkVillageHostileActionStarted);
        messageBroker.Unsubscribe<NetworkPromptRaidContinuation>(Handle_NetworkPromptRaidContinuation);
        messageBroker.Unsubscribe<NetworkVillageHostileActionDenied>(Handle_NetworkVillageHostileActionDenied);
        messageBroker.Unsubscribe<NetworkVillageHostileActionCooldowns>(Handle_NetworkVillageHostileActionCooldowns);
        messageBroker.Unsubscribe<NetworkAuthorizeForceTransfer>(Handle_NetworkAuthorizeForceTransfer);
        messageBroker.Unsubscribe<NetworkDenyForceTransfer>(Handle_NetworkDenyForceTransfer);
    }

    private void Handle_VillageHostileActionAttempted(MessagePayload<VillageHostileActionAttempted> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetIdWithLogging(message.MobileParty, out var mobilePartyId)) return;
        if (!objectManager.TryGetIdWithLogging(message.Settlement, out var settlementId)) return;
        // A request is already pending, the server still resolves the first.
        if (!clientVillageHostileActionTracker.TryBeginRequest())
        {
            Logger.Information("Dropping hostile action request: one is already pending");
            return;
        }

        network.SendAll(new NetworkRequestVillageHostileAction(message.Action, mobilePartyId, settlementId, controllerIdProvider.ControllerId));
    }

    private void Handle_NetworkVillageHostileActionStarted(MessagePayload<NetworkVillageHostileActionStarted> payload)
    {
        GameThread.RunSafe(
            () =>
            {
                clientVillageHostileActionTracker.Complete();
                if (payload.What.Unresisted) villageHostileActionInterface.ReturnToVillageMenu();
                else villageHostileActionInterface.BeginHostileActionPresentation(payload.What.Action);
            },
            context: nameof(Handle_NetworkVillageHostileActionStarted));
    }

    private void Handle_NetworkPromptRaidContinuation(MessagePayload<NetworkPromptRaidContinuation> payload)
    {
        GameThread.RunSafe(
            () =>
            {
                if (objectManager.TryGetObjectWithLogging<Settlement>(payload.What.SettlementId, out var settlement))
                    clientVillageHostileActionTracker.ParkRaidContinuation(settlement);
            },
            context: nameof(Handle_NetworkPromptRaidContinuation));
    }

    private void Handle_NetworkVillageHostileActionDenied(MessagePayload<NetworkVillageHostileActionDenied> payload)
    {
        GameThread.RunSafe(
            () =>
            {
                clientVillageHostileActionTracker.CompleteDenied();
            },
            context: nameof(Handle_NetworkVillageHostileActionDenied));
        messageBroker.Publish(this, new SendInformationMessage(GetDeniedMessage(payload.What.Reason)));
    }

    private void Handle_NetworkVillageHostileActionCooldowns(MessagePayload<NetworkVillageHostileActionCooldowns> payload)
    {
        GameThread.RunSafe(
            () => villageHostileActionInterface.ApplyCooldowns(payload.What.Cooldowns),
            context: nameof(Handle_NetworkVillageHostileActionCooldowns));
    }

    private void Handle_NetworkAuthorizeForceTransfer(MessagePayload<NetworkAuthorizeForceTransfer> payload)
    {
        var pool = payload.What.Pool;
        GameThread.RunSafe(
            () => villageHostileActionInterface.OpenForceTransferLootScreen(pool),
            context: nameof(Handle_NetworkAuthorizeForceTransfer));
    }

    private void Handle_NetworkDenyForceTransfer(MessagePayload<NetworkDenyForceTransfer> payload)
    {
        messageBroker.Publish(this, new SendInformationMessage("The village has nothing left to give."));
    }

    private static string GetDeniedMessage(VillageHostileActionDeniedReason reason)
    {
        return reason switch
        {
            VillageHostileActionDeniedReason.InvalidRequester => "Unable to start hostile action: the selected party is not controlled by you.",
            VillageHostileActionDeniedReason.NonVillageSettlement => "Unable to start hostile action: the target is not a village.",
            VillageHostileActionDeniedReason.OwnFaction => "Unable to start hostile action against your own faction.",
            VillageHostileActionDeniedReason.AlreadyInMapEvent => "Unable to start hostile action: the party or village is already in an encounter.",
            VillageHostileActionDeniedReason.InvalidVillageState => "Unable to start hostile action: the village is not in a valid state.",
            VillageHostileActionDeniedReason.HearthTooLow => "Unable to force volunteers: the village hearth is too low.",
            VillageHostileActionDeniedReason.Cooldown => "Unable to start hostile action: the village is recovering from a recent hostile action.",
            VillageHostileActionDeniedReason.NotApproved => "Unable to start hostile action: the server has not approved it.",
            _ => "Unable to start hostile action."
        };
    }
}
