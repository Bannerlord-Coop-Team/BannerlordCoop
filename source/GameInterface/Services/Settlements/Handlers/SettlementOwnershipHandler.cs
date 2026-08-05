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
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeSettlementOwnership>(Handle);
            messageBroker.Unsubscribe<SettlementGiftRequested>(Handle);
            messageBroker.Unsubscribe<NetworkRequestSettlementOwnership>(Handle);
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
        private void Handle(MessagePayload<NetworkRequestSettlementOwnership> obj)
        {
            if (!ModInformation.IsServer) return;
            if (!(obj.Who is NetPeer peer)) return;

            var payload = obj.What;
            if (!playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObject(player.HeroId, out Hero requestingHero))
            {
                Logger.Warning("Settlement gift rejected: the requesting player could not be identified");
                return;
            }

            if (!objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) ||
                !objectManager.TryGetObject(payload.NewOwnerId, out Hero newOwner))
            {
                Logger.Warning("Settlement gift rejected: settlement {Settlement} or hero {Hero} is unknown",
                    payload.SettlementId, payload.NewOwnerId);
                return;
            }

            // Cheap pre-filter, so an obviously invalid request never reaches the game thread at all.
            if (!CanGift(requestingHero, settlement, newOwner, out var reason))
            {
                Logger.Warning("Settlement gift of {Settlement} by {Hero} rejected: {Reason}",
                    settlement.StringId, requestingHero.StringId, reason);
                return;
            }

            // ApplyByGift re-enters ChangeOwnerOfSettlementPatch on the server, which publishes
            // SettlementOwnershipChanged and replicates to every client.
            GameThread.RunSafe(
                () =>
                {
                    // Re-derived here as well, because the check above ran on the network thread and this
                    // action only runs once the game thread gets to it. In between, ownership or kingdom
                    // membership can change - the fief can be captured, sold, or the clan can leave the
                    // realm - and the queued request would then transfer a settlement the requester is no
                    // longer entitled to give away. This is the check that actually guards the transfer.
                    if (!CanGift(requestingHero, settlement, newOwner, out var lateReason))
                    {
                        Logger.Warning(
                            "Settlement gift of {Settlement} by {Hero} rejected on apply: {Reason}",
                            settlement.StringId, requestingHero.StringId, lateReason);
                        return;
                    }

                    ApplyGiftRelationBonus(settlement, newOwner);
                    ChangeOwnerOfSettlementAction.ApplyByGift(settlement, newOwner);
                },
                context: nameof(NetworkRequestSettlementOwnership));
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

            // Either the owning clan's leader gives away their own fief, or the kingdom's ruler
            // grants one held by their realm - the two cases vanilla's Give Settlement covers.
            var kingdom = ownerClan.Kingdom;
            bool isOwner = ownerClan.Leader == requestingHero;
            bool isRuler = kingdom != null && kingdom.Leader == requestingHero;
            if (!isOwner && !isRuler)
            {
                reason = "the requester neither owns the settlement nor rules its kingdom";
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
