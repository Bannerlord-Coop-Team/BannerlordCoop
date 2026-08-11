using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
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
        public SettlementOwnershipHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeSettlementOwnership>(Handle);
#if DEBUG
            messageBroker.Subscribe<NetworkPrepareMissingSettlementOwnerFixture>(Handle);
#endif
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeSettlementOwnership>(Handle);
#if DEBUG
            messageBroker.Unsubscribe<NetworkPrepareMissingSettlementOwnerFixture>(Handle);
#endif
        }

        private void Handle(MessagePayload<SettlementOwnershipChanged> obj)
        {
            var payload = obj.What;

            var message = new NetworkChangeSettlementOwnership(
                payload.SettlementId,
                payload.OwnerId,
                payload.PreviousOwnerId,
                payload.CapturerId,
                payload.Detail);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkChangeSettlementOwnership> obj)
        {
            var payload = obj.What;

            // Resolve in queue order with deferred ownership-object creation.
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement)) return;
                if (!objectManager.TryGetObjectWithLogging(payload.OwnerId, out Hero owner)) return;
                if (!objectManager.TryGetObjectWithLogging(payload.PreviousOwnerId, out Hero previousOwner)) return;

                Hero capturer = null;
                if (payload.CapturerId != null &&
                    !objectManager.TryGetObjectWithLogging(payload.CapturerId, out capturer)) return;

                var detail = (ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail)payload.Detail;

                // Apply only the direct owner change. The action's other side effects (patrol
                // culling, garrison destruction and creation, governor removal) run on the server
                // with patches live and arrive here as their own replicated messages; replaying the
                // whole action would apply them a second time.
                using (new AllowedThread())
                {
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
                        settlement, openToClaim, owner, previousOwner, capturer, detail);
                }
            }, context: nameof(NetworkChangeSettlementOwnership));
        }

#if DEBUG
        private void Handle(MessagePayload<NetworkPrepareMissingSettlementOwnerFixture> obj)
        {
            var payload = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement)) return;
                if (!settlement.IsFortification) return;

                using (new AllowedThread())
                {
                    settlement.Town.OwnerClan = null;
                }
            }, context: nameof(NetworkPrepareMissingSettlementOwnerFixture));
        }
#endif
    }
}
