using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Common.Extensions;

namespace GameInterface.Services.Kingdoms.Handlers
{
    public class KingdomDecisionHandler : IHandler
    {
        private static Func<Kingdom, MBList<KingdomDecision>> GetUnresolvedDecisions = typeof(Kingdom).GetField("_unresolvedDecisions", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Kingdom, MBList<KingdomDecision>>();
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

            var decisions = GetUnresolvedDecisions(kingdom);
            if (payload.Index >= 0 && decisions.Count > payload.Index)
            {
                KingdomPatches.RunOriginalRemoveDecision(kingdom, decisions[payload.Index]);
            }
            else
            {
                Logger.Verbose("Index is out of bounds of the list.");
                return;
            }
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
