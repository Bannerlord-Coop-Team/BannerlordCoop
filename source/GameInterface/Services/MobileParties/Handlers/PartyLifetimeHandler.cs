using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.GameState.Messages;
using System;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobileParties.Patches;

namespace GameInterface.Services.MobileParties.Handlers
{
    /// <summary>
    /// Listens to Bannerlord party lifecycle events and relays them to the <see cref="MessageBroker"/> system.
    /// </summary>
    internal class PartyLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimeHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public PartyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager) 
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<CampaignStateEntered>(Handle_CampaignStateEntered);

            messageBroker.Subscribe<CreateParty>(Handle_CreateParty);
            messageBroker.Subscribe<DestroyParty>(Handle_DestroyParty);
        }

        private void Handle_DestroyParty(MessagePayload<DestroyParty> payload)
        {
            var stringId = payload.What.Data.StringId;

            PartyCreationDeletionPatches.OverrideRemoveParty(stringId);
        }

        private void Handle_CreateParty(MessagePayload<CreateParty> payload)
        {
            var stringId = payload.What.Data.StringId;

            PartyCreationDeletionPatches.OverrideCreateNewParty(stringId);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CampaignStateEntered>(Handle_CampaignStateEntered);;

            if (Campaign.Current == null)
                return;

            CampaignEvents.MobilePartyCreated.ClearListeners(this);
        }

        public void RegisterPartyListeners()
        {
            if (Campaign.Current == null)
            {
                Logger.Warning("Unable to register party life-cycle listeners, no active campaign");
                return;
            }

            //CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, Handle_MobilePartyCreated);
            //CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, Handle_MobilePartyDestroyed);
        }

        public void Handle_CampaignStateEntered(MessagePayload<CampaignStateEntered> obj)
        {
            RegisterPartyListeners();
        }
    }
}
