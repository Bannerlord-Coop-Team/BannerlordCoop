using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.GameState.Messages;
using System;

namespace GameInterface.Services.MobileParties.Handlers
{
    /// <summary>
    /// Listens to Bannerlord party lifecycle events and relays them to the <see cref="MessageBroker"/> system.
    /// </summary>
    internal class MobilePartyLifecycleHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyLifecycleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IMobilePartyRegistry mobilePartyRegistry;

        public MobilePartyLifecycleHandler(IMessageBroker messageBroker, IMobilePartyRegistry mobilePartyRegistry) 
        {
            this.messageBroker = messageBroker;
            this.mobilePartyRegistry = mobilePartyRegistry;

            messageBroker.Subscribe<CampaignStateEntered>(Handle_CampaignStateEntered);
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

            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, Handle_MobilePartyCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, Handle_MobilePartyDestroyed);
        }

        public void Handle_CampaignStateEntered(MessagePayload<CampaignStateEntered> obj)
        {
            RegisterPartyListeners();
        }

        public void Handle_MobilePartyCreated(MobileParty party)
        {
            mobilePartyRegistry.RegisterExistingObject(party.StringId, party);

            messageBroker.Publish(this, new MobilePartyCreated(party));

            Logger.Verbose("Created party from {callstack}", Environment.StackTrace);
        }

        public void Handle_MobilePartyDestroyed(MobileParty party, PartyBase partyBase)
        {
            mobilePartyRegistry.Remove(party);

            messageBroker.Publish(this, new MobilePartyDestroyed(party, partyBase));

            Logger.Verbose("Destroyed party from {callstack}", Environment.StackTrace);
        }
    }
}
