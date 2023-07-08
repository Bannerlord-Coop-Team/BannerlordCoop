using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
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

        public KingdomUpdateHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<UpdatedKingdomRelation>(Handle);
            messageBroker.Subscribe<PolicyAdded>(Handle);
            messageBroker.Subscribe<PolicyRemoved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UpdatedKingdomRelation>(Handle);
            messageBroker.Unsubscribe<PolicyAdded>(Handle);
            messageBroker.Unsubscribe<PolicyRemoved>(Handle);
        }

        private void Handle(MessagePayload<UpdatedKingdomRelation> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);
            Kingdom kingdom = Kingdom.All.Find(x => x.StringId == payload.KingdomId);

            ChangeKingdomActionPatch.RunOriginalApplyInternal(clan, kingdom, (ChangeKingdomActionDetail)payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);
        }
        private void Handle(MessagePayload<PolicyAdded> obj)
        {
            var payload = obj.What;

            PolicyObject policy = new PolicyObject(payload.PolicyId);
            Kingdom kingdom = Kingdom.All.Find(x => x.StringId == payload.KingdomId);

            KingdomAddPolicyPatch.RunOriginalAddPolicy(policy, kingdom);

        }

        private void Handle(MessagePayload<PolicyRemoved> obj)
        {
            var payload = obj.What;

            PolicyObject policy = new PolicyObject(payload.PolicyId);
            Kingdom kingdom = Kingdom.All.Find(x => x.StringId == payload.KingdomId);

            KingdomRemovePolicyPatch.RunOriginalRemovePolicy(policy, kingdom);

        }
    }
}
