using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

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
            ClearItemRoster msg = payload.What;

            ItemRoster roster;
            if (!objectManager.TryGetObjectWithLogging(msg.PartyBaseID, out PartyBase partyBase)) return;

            ItemRosterPatch.ClearOverride(partyBase.ItemRoster);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClearItemRoster>(Handle);
        }
    }
}
