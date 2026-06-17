using Common;
using Common.Logging;
using Common.Messaging;
using System;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

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

        public ClearItemRosterHandler(IMessageBroker messageBroker, IObjectManager objectManager) {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

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

                    ItemRosterPatch.ClearOverride(itemRoster);
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
