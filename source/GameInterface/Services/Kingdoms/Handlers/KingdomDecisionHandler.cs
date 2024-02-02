using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Handlers
{
    public class KingdomDecisionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private static readonly ILogger Logger = LogManager.GetLogger<KingdomDecisionHandler>();

        public KingdomDecisionHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<AddDecision>(HandleAddDecision);
            messageBroker.Subscribe<RemoveDecision>(HandleRemoveDecision);
        }

        private void HandleRemoveDecision(MessagePayload<RemoveDecision> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
            {
                Logger.Verbose("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
                return;
            }

            if (!payload.Data.TryGetKingdomDecision(objectManager, out KingdomDecision kingdomDecision))
            {
                Logger.Verbose("KingdomDecision could not be deserialized in KingdomDecisionHandler.");
                return;
            }

            KingdomPatches.RunOriginalRemoveDecision(kingdom, kingdomDecision);
        }

        private void HandleAddDecision(MessagePayload<AddDecision> obj)
        {
            var payload = obj.What;

            if(!objectManager.TryGetObject(payload.KingdomId, out Kingdom kingdom))
            {
                Logger.Verbose("Kingdom not found in KingdomDecisionHandler with KingdomId: {id}", payload.KingdomId);
                return;
            }

            if (!payload.Data.TryGetKingdomDecision(objectManager, out KingdomDecision kingdomDecision))
            {
                Logger.Verbose("KingdomDecision could not be deserialized in KingdomDecisionHandler.");
                return;
            }

            KingdomPatches.RunOriginalAddDecision(kingdom, kingdomDecision, payload.IgnoreInfluenceCost);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AddDecision>(HandleAddDecision);
            messageBroker.Unsubscribe<RemoveDecision>(HandleRemoveDecision);
        }
    }
}
