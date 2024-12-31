using Autofac.Features.OwnedInstances;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
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
        private static readonly ILogger Logger = LogManager.GetLogger<SettlementOwnershipHandler>();

        public SettlementOwnershipHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeSettlementOwnership>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementOwnershipChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeSettlementOwnership>(Handle);
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

            ChangeOwnerOfSettlementPatch.RunOriginalApplyInternal(settlement, owner, capturer,
                (ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail)payload.Detail);
        }
    }
}
