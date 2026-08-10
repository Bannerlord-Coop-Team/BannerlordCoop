using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using GameInterface.Services.Settlements.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Settlements.Handlers
{
    /// <summary>
    /// GameInterface Settlement Ownership handler
    /// </summary>
    public class SettlementOwnershipHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IPlayerManager playerManager;
        private static readonly ILogger Logger = LogManager.GetLogger<SettlementOwnershipHandler>();

        public SettlementOwnershipHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network, IPlayerManager playerManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.playerManager = playerManager;
            messageBroker.Subscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeSettlementOwnership>(Handle);
            messageBroker.Subscribe<SettlementGiftRequested>(Handle);
            messageBroker.Subscribe<NetworkRequestSettlementOwnership>(Handle);
            messageBroker.Subscribe<NetworkSettlementGiftRejected>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeSettlementOwnership>(Handle);
            messageBroker.Unsubscribe<SettlementGiftRequested>(Handle);
            messageBroker.Unsubscribe<NetworkRequestSettlementOwnership>(Handle);
            messageBroker.Unsubscribe<NetworkSettlementGiftRejected>(Handle);
        }

        /// <summary>Client side: forward the player's gift to the server.</summary>
        private void Handle(MessagePayload<SettlementGiftRequested> obj)
        {
            if (!ModInformation.IsClient) return;

            var payload = obj.What;
            if (!objectManager.TryGetIdWithLogging(payload.Settlement, out var settlementId)) return;
            if (!objectManager.TryGetIdWithLogging(payload.NewOwner, out var newOwnerId)) return;

            network.SendAll(new NetworkRequestSettlementOwnership(settlementId, newOwnerId));
        }

        /// <summary>
        /// Server side: a client asked to gift a settlement. Authority is re-derived here - the
        /// request carries only two ids and is never trusted for who may give what away.
        /// </summary>
        /// <remarks>
        /// Everything runs on the game thread, resolution included. Resolving ids and testing authority
        /// reads campaign state - OwnerClan, Kingdom, IsAlive - and doing that on the network thread both
        /// races the game loop and judges the request against a world that a queued action may be about
        /// to change, so a request could be refused on state that was already stale when it was read.
        /// </remarks>
        private void Handle(MessagePayload<NetworkRequestSettlementOwnership> obj)
        {
            if (!ModInformation.IsServer) return;
            if (!(obj.Who is NetPeer peer)) return;

            var payload = obj.What;

            GameThread.RunSafe(
                () => ProcessGiftRequest(peer, payload),
                context: nameof(NetworkRequestSettlementOwnership));
        }

        /// <summary>[Server, game thread] Validates and applies a gift, telling the requester on refusal.</summary>
        private void ProcessGiftRequest(NetPeer peer, NetworkRequestSettlementOwnership payload)
        {
            if (!playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObject(player.HeroId, out Hero requestingHero))
            {
                RejectGift(peer, "The server could not identify the requesting player.",
                    "Settlement gift rejected: the requesting player could not be identified");
                return;
            }

            if (!objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) ||
                !objectManager.TryGetObject(payload.NewOwnerId, out Hero newOwner))
            {
                RejectGift(peer, "That settlement or recipient is no longer available.",
                    "Settlement gift rejected: settlement {Settlement} or hero {Hero} is unknown",
                    payload.SettlementId, payload.NewOwnerId);
                return;
            }

            if (!CanGift(requestingHero, settlement, newOwner, out var reason))
            {
                RejectGift(peer, $"The settlement could not be given: {reason}.",
                    "Settlement gift of {Settlement} by {Hero} rejected: {Reason}",
                    settlement.StringId, requestingHero.StringId, reason);
                return;
            }

            // ApplyByGift re-enters ChangeOwnerOfSettlementPatch on the server, which publishes
            // SettlementOwnershipChanged and replicates to every client.
            ApplyGiftRelationBonus(settlement, newOwner);
            ChangeOwnerOfSettlementAction.ApplyByGift(settlement, newOwner);
        }

        /// <summary>
        /// Logs the refusal and tells the requester, so a late failure is not the same silent no-op
        /// this feature exists to fix.
        /// </summary>
        private void RejectGift(NetPeer peer, string playerReason, string logTemplate, params object[] logArgs)
        {
            Logger.Warning(logTemplate, logArgs);
            network.Send(peer, new NetworkSettlementGiftRejected(playerReason));
        }

        /// <summary>Client side: surface a refused gift, which the kingdom screen has already closed.</summary>
        /// <remarks>
        /// Marshalled onto the game thread. This handler runs on the network poll thread, and
        /// <c>InformationManager.DisplayMessage</c> touches the UI message queue - calling it from the poller
        /// races the main loop, which is the same class of bug as reading Campaign state off-thread in
        /// <see cref="ProcessGiftRequest"/>.
        /// </remarks>
        private void Handle(MessagePayload<NetworkSettlementGiftRejected> obj)
        {
            if (!ModInformation.IsClient) return;

            var reason = obj.What.Reason;
            if (string.IsNullOrWhiteSpace(reason)) reason = "The settlement could not be given.";

            GameThread.RunSafe(
                () => InformationManager.DisplayMessage(new InformationMessage(reason)),
                context: nameof(NetworkSettlementGiftRejected));
        }

        /// <summary>
        /// Applies the relation bonus vanilla grants for gifting a fief, which ApplyByGift alone does not.
        /// </summary>
        /// <remarks>
        /// Vanilla routes a player gift through <c>KingdomManager.GiftSettlementOwnership</c>, which first
        /// calls <c>ChangeRelationAction.ApplyRelationChangeBetweenHeroes(settlement.OwnerClan.Leader,
        /// receiver.Leader, bonus, true)</c> with DiplomacyModel's GiftingTownRelationshipBonus for a town or
        /// GiftingCastleRelationshipBonus otherwise, and only then transfers ownership. Co-op calls
        /// ChangeOwnerOfSettlementAction.ApplyByGift directly, so the granting player got the fief cost with
        /// none of the goodwill. Runs on the server with patches live, so the relation change replicates.
        /// </remarks>
        private static void ApplyGiftRelationBonus(Settlement settlement, Hero newOwner)
        {
            var giver = settlement?.OwnerClan?.Leader;
            var receiver = newOwner?.Clan?.Leader;
            if (giver == null || receiver == null || giver == receiver) return;

            var diplomacy = Campaign.Current?.Models?.DiplomacyModel;
            if (diplomacy == null) return;

            var bonus = settlement.IsTown
                ? diplomacy.GiftingTownRelationshipBonus
                : diplomacy.GiftingCastleRelationshipBonus;

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(giver, receiver, bonus, true);
        }

        private static bool CanGift(Hero requestingHero, Settlement settlement, Hero newOwner, out string reason)
        {
            if (!requestingHero.IsAlive || !newOwner.IsAlive)
            {
                reason = "a participant is dead";
                return false;
            }

            var ownerClan = settlement.OwnerClan;
            if (ownerClan == null)
            {
                reason = "the settlement has no owner clan";
                return false;
            }

            // Ownership AND rulership, because vanilla gates the gift twice, in sequence:
            //
            //   KingdomSettlementVM.ExecuteAnnex   settlement.OwnerClan.Leader == Hero.MainHero
            //                                      -> _onGrantFief, else the ANNEX action (costs influence)
            //   KingdomManagementVM.OnGrantFief    Kingdom.Leader == Hero.MainHero
            //                                      -> GiftFief.OpenWith(settlement)
            //                                      else "give this settlement back to your kingdom"
            //                                           (RelinquishSettlementOwnership - a different action)
            //
            // So an owner who does not rule never reaches the gift popup at all; they are offered relinquish
            // instead. Checking ownership alone let a vassal perform a transfer vanilla does not offer them.
            //
            // Ownership is still required, and still closes the replay hole: once the gift lands the giver no
            // longer owns the fief, so a stale duplicate request finds a different owner and is refused rather
            // than moving the settlement a second time.
            var kingdom = ownerClan.Kingdom;
            if (ownerClan.Leader != requestingHero)
            {
                reason = "the requester does not own the settlement";
                return false;
            }

            if (kingdom == null || kingdom.Leader != requestingHero)
            {
                reason = "the requester does not rule the kingdom";
                return false;
            }

            if (newOwner.Clan == null || newOwner.Clan.Kingdom != kingdom)
            {
                reason = "the recipient is not in the same kingdom";
                return false;
            }

            reason = null;
            return true;
        }

        private void Handle(MessagePayload<SettlementOwnershipChanged> obj)
        {
            var payload = obj.What;

            var message = new NetworkChangeSettlementOwnership(
                payload.SettlementId,
                payload.OwnerId,
                payload.CapturerId,
                payload.Detail);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkChangeSettlementOwnership> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false)
            {
                Logger.Verbose("Settlement not found in SettlementHandler with SettlementId: {id}", payload.SettlementId);
                return;
            }

            if (objectManager.TryGetObject(payload.OwnerId, out Hero owner) == false)
            {
                Logger.Verbose("Owner not found in SettlementHandler with OwnerId: {id}", payload.OwnerId);
                return;
            }

            if (objectManager.TryGetObject(payload.CapturerId, out Hero capturer) == false && payload.CapturerId != null)
            {
                Logger.Verbose("Capturer not found in SettlementHandler with CapturerId: {id}", payload.CapturerId);
                return;
            }

            var detail = (ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail)payload.Detail;

            // Apply only the direct owner change. The action's other side effects (patrol
            // culling, garrison destruction and creation, governor removal) run on the server
            // with patches live and arrive here as their own replicated messages; replaying the
            // whole action would apply them a second time.
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    var oldOwner = settlement.OwnerClan?.Leader;

                    if (settlement.Town != null)
                    {
                        settlement.Town.IsOwnerUnassigned = false;
                    }

                    if (settlement.IsFortification)
                    {
                        settlement.Town.OwnerClan = owner.Clan;
                    }

                    settlement.Party.SetVisualAsDirty();
                    foreach (var boundVillage in settlement.BoundVillages)
                    {
                        boundVillage.Settlement.Party.SetVisualAsDirty();
                    }

                    // Fire the owner-changed event so client-side listeners (map notifications,
                    // UI refreshes, the claimant behavior's bookkeeping) still react — the same
                    // listeners the old full replay reached. Server-side behaviors with game
                    // consequences (patrol culling etc.) are disabled on clients and stay silent.
                    var openToClaim = (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege
                        || detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByClanDestruction
                        || detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByLeaveFaction)
                        && settlement.IsFortification;

                    CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(
                        settlement, openToClaim, owner, oldOwner, capturer, detail);
                }
            });
        }
    }
}
