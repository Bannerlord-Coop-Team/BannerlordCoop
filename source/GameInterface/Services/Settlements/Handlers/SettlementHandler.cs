using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers
{
    /// <summary>
    /// GameInterface Settlement handler
    /// </summary>
    public class SettlementHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private static readonly ILogger Logger = LogManager.GetLogger<SettlementHandler>();

        public SettlementHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ChangeSettlementOwnership>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeSettlementOwnership>(Handle);
        }

        private void Handle(MessagePayload<ChangeSettlementOwnership> obj)
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
