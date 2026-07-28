using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Handlers
{
    /// <summary>
    /// Handles ClearItemRoster.
    /// </summary>
    internal class ClearItemRosterHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ClearItemRosterHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly IPartyScreenRosterBaselineProvider partyScreenRosterBaselineProvider;

        public ClearItemRosterHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            IPartyScreenRosterBaselineProvider partyScreenRosterBaselineProvider) {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.partyScreenRosterBaselineProvider = partyScreenRosterBaselineProvider;

            messageBroker.Subscribe<ClearItemRoster>(Handle);
        }

        public void Handle(MessagePayload<ClearItemRoster> payload)
        {
            var data = payload.What;

            GameThread.Run(() =>
            {
                try
                {
                    if (!objectManager.TryGetObjectWithLogging<ItemRoster>(data.ItemRosterId, out var itemRoster)) return;

                    var logic = (Game.Current?.GameStateManager?.ActiveState as PartyState)?.PartyScreenLogic;
                    var baseline = partyScreenRosterBaselineProvider.GetBaselineRoster(logic, itemRoster);
                    ItemRosterPatch.ClearOverride(itemRoster);
                    if (baseline != null) ItemRosterPatch.ClearOverride(baseline);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply ClearItemRoster");
                }
            });
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClearItemRoster>(Handle);
        }
    }
}
