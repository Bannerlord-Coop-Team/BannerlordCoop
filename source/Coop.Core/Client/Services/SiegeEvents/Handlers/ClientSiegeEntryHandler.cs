using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.SiegeEvents.Handlers;

/// <summary>
/// Sends the local player's siege entry and exit requests to the server and runs the player-local
/// menu continuation when the approval arrives.
/// </summary>
internal class ClientSiegeEntryHandler : IHandler
{
    private static readonly Serilog.ILogger Logger = LogManager.GetLogger<ClientSiegeEntryHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    public ClientSiegeEntryHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        messageBroker.Subscribe<BesiegeSettlementAttempted>(HandleBesiegeAttempt);
        messageBroker.Subscribe<JoinSiegeCampAttempted>(HandleJoinAttempt);
        messageBroker.Subscribe<BreakSiegeAttempted>(HandleBreakAttempt);
        messageBroker.Subscribe<NetworkBesiegeSettlementApproved>(HandleBesiegeApproved);
        messageBroker.Subscribe<NetworkJoinSiegeCampApproved>(HandleJoinApproved);
        messageBroker.Subscribe<NetworkBreakSiegeApproved>(HandleBreakApproved);
        messageBroker.Subscribe<NetworkPromptSiegeDefense>(HandleDefensePrompt);
        messageBroker.Subscribe<NetworkPromptSiegePreparation>(HandlePreparationPrompt);
        messageBroker.Subscribe<NetworkPromptSiegeEnded>(HandleSiegeEndedPrompt);
        messageBroker.Subscribe<AssaultSiegeAttempted>(HandleAssaultAttempt);
        messageBroker.Subscribe<NetworkPromptSiegeAssault>(HandleAssaultPrompt);
        messageBroker.Subscribe<NetworkSnapSiegeCampPartyPosition>(HandleCampPositionSnap);
    }

    private void HandleCampPositionSnap(MessagePayload<NetworkSnapSiegeCampPartyPosition> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var party)) return;

            using (new AllowedThread())
            {
                party.Position = obj.Position;
            }
        });
    }

    private void HandlePreparationPrompt(MessagePayload<NetworkPromptSiegePreparation> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptSiegePreparation(attackerParty, settlement);
        });
    }

    private void HandleSiegeEndedPrompt(MessagePayload<NetworkPromptSiegeEnded> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptSiegeEnded(settlement, obj.BesiegerDefeated);
        });
    }

    private void HandleDefensePrompt(MessagePayload<NetworkPromptSiegeDefense> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            // No AllowedThread wrapper: the method scopes it per section, so the non-joinable
            // defender's settlement leave routes through the normal co-op leave flow.
            siegeEventInterface.PromptSiegeDefense(attackerParty, settlement);
        });
    }

    // Runs on the game thread already — SiegeEntryFlowPatches publishes AssaultSiegeAttempted from the assault
    // menu consequence, and this only resolves ids and sends the request, so no GameThread.RunSafe is needed.
    private void HandleAssaultAttempt(MessagePayload<AssaultSiegeAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRequestSiegeAssault(partyId, settlementId));
    }

    private void HandleAssaultPrompt(MessagePayload<NetworkPromptSiegeAssault> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptSiegeAssault(attackerParty, settlement);
        });
    }

    // Runs on the game thread already — SiegeEntryFlowPatches publishes the *Attempted message from the besiege menu consequence, and this only resolves ids and sends the request, so no GameThread.RunSafe is needed.
    private void HandleBesiegeAttempt(MessagePayload<BesiegeSettlementAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRequestBesiegeSettlement(partyId, settlementId));
    }

    // Runs on the game thread already — published from the join-siege menu consequence; only resolves ids and sends, so no GameThread.RunSafe.
    private void HandleJoinAttempt(MessagePayload<JoinSiegeCampAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRequestJoinSiegeCamp(partyId, settlementId));
    }

    // Runs on the game thread already — published from the leave-siege consequence; only resolves an id and sends, so no GameThread.RunSafe.
    private void HandleBreakAttempt(MessagePayload<BreakSiegeAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;

        network.SendAll(new NetworkRequestBreakSiege(partyId));
    }

    private void HandleBesiegeApproved(MessagePayload<NetworkBesiegeSettlementApproved> payload)
    {
        if (!payload.What.Approved)
        {
            Logger.Information("Server rejected the besiege request; staying at the current menu");
            return;
        }

        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                siegeEventInterface.StartLocalPlayerSiegePreparation();
            }
        });
    }

    private void HandleJoinApproved(MessagePayload<NetworkJoinSiegeCampApproved> payload)
    {
        var obj = payload.What;

        if (!obj.Approved)
        {
            Logger.Information("Server rejected the join-siege request; staying at the current menu");
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            using (new AllowedThread())
            {
                siegeEventInterface.StartLocalPlayerJoinedSiege(settlement);
            }
        });
    }

    private void HandleBreakApproved(MessagePayload<NetworkBreakSiegeApproved> payload)
    {
        if (!payload.What.Approved)
        {
            Logger.Information("Server rejected the break-siege request; staying at the current menu");
            return;
        }

        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                siegeEventInterface.FinishLocalPlayerSiegeLeave();
            }
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BesiegeSettlementAttempted>(HandleBesiegeAttempt);
        messageBroker.Unsubscribe<JoinSiegeCampAttempted>(HandleJoinAttempt);
        messageBroker.Unsubscribe<BreakSiegeAttempted>(HandleBreakAttempt);
        messageBroker.Unsubscribe<NetworkBesiegeSettlementApproved>(HandleBesiegeApproved);
        messageBroker.Unsubscribe<NetworkJoinSiegeCampApproved>(HandleJoinApproved);
        messageBroker.Unsubscribe<NetworkBreakSiegeApproved>(HandleBreakApproved);
        messageBroker.Unsubscribe<NetworkPromptSiegeDefense>(HandleDefensePrompt);
        messageBroker.Unsubscribe<NetworkPromptSiegePreparation>(HandlePreparationPrompt);
        messageBroker.Unsubscribe<NetworkPromptSiegeEnded>(HandleSiegeEndedPrompt);
        messageBroker.Unsubscribe<AssaultSiegeAttempted>(HandleAssaultAttempt);
        messageBroker.Unsubscribe<NetworkPromptSiegeAssault>(HandleAssaultPrompt);
        messageBroker.Unsubscribe<NetworkSnapSiegeCampPartyPosition>(HandleCampPositionSnap);
    }
}
