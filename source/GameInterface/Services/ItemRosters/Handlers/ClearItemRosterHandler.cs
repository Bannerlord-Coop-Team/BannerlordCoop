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
            if (objectManager.TryGetObject(msg.PartyBaseID, out Settlement s))
            {
                roster = s.ItemRoster;
            }
            else if (objectManager.TryGetObject(msg.PartyBaseID, out MobileParty p))
            {
                roster = p.ItemRoster;
            }
            else
            {
                Logger.Error("Failed to update item roster, no Settlement nor Party with ID '{partyBaseId}' was found", msg.PartyBaseID);
                return;
            }

            ItemRosterPatch.ClearOverride(roster);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClearItemRoster>(Handle);
        }
    }
}
