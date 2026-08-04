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
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
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
    private PendingBreakInContinuation pendingBreakInContinuation;

    internal TimeSpan BreakInContinuationTimeout { get; set; }

    public ClientSiegeEntryHandler(
        IMessageBroker messageBroker,
        INetwork network,
        INetworkConfig configuration,
        IObjectManager objectManager,
        ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        BreakInContinuationTimeout = configuration.ObjectCreationTimeout;
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
        messageBroker.Subscribe<BreakInContinuationAttempted>(HandleBreakInContinuationAttempt);
        messageBroker.Subscribe<NetworkBreakInContinuationApproved>(HandleBreakInContinuationApproved);
    }

    private void HandleBreakInContinuationAttempt(MessagePayload<BreakInContinuationAttempted> payload)
    {
        var now = DateTime.UtcNow;
        var pending = pendingBreakInContinuation;
        if (pending != null)
        {
            if (pending.ExpiresAtUtc > now)
            {
                Logger.Information(
                    "Ignoring break-in continuation attempt while request {RequestId} is pending",
                    pending.RequestId);
                return;
            }

            Logger.Warning(
                "Retrying break-in continuation after request {RequestId} timed out",
                pending.RequestId);
            ClearPendingBreakInContinuation(pending, restoreLocationEncounter: true);
        }

        var obj = payload.What;
        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        var requestId = Guid.NewGuid().ToString();
        var previousLocationEncounter = PlayerEncounter.LocationEncounter;
        siegeEventInterface.PrepareLocalPlayerBreakIn(obj.Settlement);
        var stagedLocationEncounter = PlayerEncounter.LocationEncounter;
        pendingBreakInContinuation = new PendingBreakInContinuation(
            requestId,
            settlementId,
            PlayerEncounter.Current,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
            previousLocationEncounter,
            stagedLocationEncounter,
            now + BreakInContinuationTimeout);

        network.SendAll(new NetworkRequestBreakInContinuation(requestId, partyId, settlementId));
    }

    private void HandleBreakInContinuationApproved(MessagePayload<NetworkBreakInContinuationApproved> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            var pending = pendingBreakInContinuation;
            if (pending == null ||
                pending.RequestId != obj.RequestId ||
                pending.SettlementId != obj.SettlementId)
                return;

            if (!obj.Approved)
            {
                var rejectionMenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
                var shouldRecoverFromDebrief =
                    ReferenceEquals(PlayerEncounter.Current, pending.Encounter) &&
                    rejectionMenuId == pending.MenuId;
                ClearPendingBreakInContinuation(pending, restoreLocationEncounter: true);
                if (shouldRecoverFromDebrief)
                {
                    siegeEventInterface.FinishLocalPlayerSiegeLeave();
                    Logger.Information("Server rejected the break-in continuation; returning to the campaign map");
                }
                else
                {
                    Logger.Information("Server rejected the break-in continuation after the encounter changed");
                }
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement))
            {
                ClearPendingBreakInContinuation(pending, restoreLocationEncounter: true);
                return;
            }

            var currentMenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
            if (!ReferenceEquals(PlayerEncounter.Current, pending.Encounter) ||
                !ReferenceEquals(PlayerEncounter.EncounterSettlement, settlement) ||
                currentMenuId != pending.MenuId)
            {
                ClearPendingBreakInContinuation(pending, restoreLocationEncounter: false);
                Logger.Warning("Ignoring break-in approval because the encounter or menu changed");
                return;
            }

            if (!ReferenceEquals(MobileParty.MainParty?.CurrentSettlement, settlement))
            {
                ClearPendingBreakInContinuation(pending, restoreLocationEncounter: true);
                Logger.Error("Ignoring break-in approval because the settlement entry was not applied");
                return;
            }

            ClearPendingBreakInContinuation(pending, restoreLocationEncounter: false);
            siegeEventInterface.ContinueLocalPlayerBreakIn(settlement);
        }, context: nameof(HandleBreakInContinuationApproved));
    }

    private void ClearPendingBreakInContinuation(
        PendingBreakInContinuation pending,
        bool restoreLocationEncounter)
    {
        if (!ReferenceEquals(pendingBreakInContinuation, pending))
            return;

        pendingBreakInContinuation = null;
        if (restoreLocationEncounter &&
            Campaign.Current != null &&
            ReferenceEquals(PlayerEncounter.LocationEncounter, pending.StagedLocationEncounter))
        {
            PlayerEncounter.LocationEncounter = pending.PreviousLocationEncounter;
        }
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

        network.SendAll(new NetworkRequestBreakSiege(partyId, obj.FinishLocalMenus));
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

        // The server routed a battle leave instead of a camp break; the returning battle-leave
        // reply owns the menu continuation.
        if (payload.What.BattleLeaveApplied)
            return;

        // Embedded camp writes (try-to-get-away, the defeat path, safe-passage barter) already ran
        // their native menu continuation; finishing here would tear down the menu they landed on.
        if (!payload.What.FinishLocalMenus) return;

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
        var pending = pendingBreakInContinuation;
        if (pending != null)
            ClearPendingBreakInContinuation(pending, restoreLocationEncounter: true);

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
        messageBroker.Unsubscribe<BreakInContinuationAttempted>(HandleBreakInContinuationAttempt);
        messageBroker.Unsubscribe<NetworkBreakInContinuationApproved>(HandleBreakInContinuationApproved);
    }

    private sealed class PendingBreakInContinuation
    {
        public readonly string RequestId;
        public readonly string SettlementId;
        public readonly PlayerEncounter Encounter;
        public readonly string MenuId;
        public readonly LocationEncounter PreviousLocationEncounter;
        public readonly LocationEncounter StagedLocationEncounter;
        public readonly DateTime ExpiresAtUtc;

        public PendingBreakInContinuation(
            string requestId,
            string settlementId,
            PlayerEncounter encounter,
            string menuId,
            LocationEncounter previousLocationEncounter,
            LocationEncounter stagedLocationEncounter,
            DateTime expiresAtUtc)
        {
            RequestId = requestId;
            SettlementId = settlementId;
            Encounter = encounter;
            MenuId = menuId;
            PreviousLocationEncounter = previousLocationEncounter;
            StagedLocationEncounter = stagedLocationEncounter;
            ExpiresAtUtc = expiresAtUtc;
        }
    }
}
