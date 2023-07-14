using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using static TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction;

namespace GameInterface.Services.Kingdoms.Handler
{
    /// <summary>
    /// Handles all changes to kingdoms.
    /// </summary>
    public class KingdomUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ILogger Logger = LogManager.GetLogger<KingdomUpdateHandler>();
        private readonly IObjectManager objectManager;

        public KingdomUpdateHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<KingdomRelationUpdated>(Handle);
            messageBroker.Subscribe<PolicyAdded>(Handle);
            messageBroker.Subscribe<PolicyRemoved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<KingdomRelationUpdated>(Handle);
            messageBroker.Unsubscribe<PolicyAdded>(Handle);
            messageBroker.Unsubscribe<PolicyRemoved>(Handle);
        }

        private void Handle(MessagePayload<KingdomRelationUpdated> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.ClanId, out Clan clan) == false)
            {
                Logger.Error("PartyId not found: {id}", payload.ClanId);
                return;
            }

            if (objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom) == false)
            {
                Logger.Information("KingdomId not found: {id}", payload.KingdomId);
            }

            ChangeKingdomActionPatch.RunOriginalApplyInternal(clan, kingdom, (ChangeKingdomActionDetail)payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);
        }
        private void Handle(MessagePayload<PolicyAdded> obj)
        {
            var payload = obj.What;

            PolicyObject policy = new PolicyObject(payload.PolicyId);

            if (objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom) == false)
            {
                Logger.Information("KingdomId not found: {id}", payload.KingdomId);
                return;
            }

            KingdomAddPolicyPatch.RunOriginalAddPolicy(policy, kingdom);

        }

        private void Handle(MessagePayload<PolicyRemoved> obj)
        {
            var payload = obj.What;

            PolicyObject policy = new PolicyObject(payload.PolicyId);

            if (objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom) == false)
            {
                Logger.Information("KingdomId not found: {id}", payload.KingdomId);
                return;
            }

            KingdomRemovePolicyPatch.RunOriginalRemovePolicy(policy, kingdom);

        }
    }
}
