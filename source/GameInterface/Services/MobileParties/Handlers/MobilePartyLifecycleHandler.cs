using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using Coop.Mod.Extentions;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Handlers
{
    // TODO combine with ObjectManager service - this was created without knowledge of the existing system.
    internal class MobilePartyLifecycleHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyLifecycleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IMobilePartyRegistry mobilePartyRegistry;

        public MobilePartyLifecycleHandler(IMessageBroker messageBroker, IMobilePartyRegistry mobilePartyRegistry) 
        {
            this.messageBroker = messageBroker;
            this.mobilePartyRegistry = mobilePartyRegistry;

            messageBroker.Subscribe<RegisterAllGameObjects>(RegisterPartyListeners);
            messageBroker.Subscribe<LoadExistingObjectGuids>(RegisterPartyListeners);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<RegisterAllGameObjects>(RegisterPartyListeners);
            messageBroker.Unsubscribe<LoadExistingObjectGuids>(RegisterPartyListeners);

            if (Campaign.Current == null)
                return;

            CampaignEvents.MobilePartyCreated.ClearListeners(this);
        }

        public void RegisterPartyListeners(dynamic _)
        {
            RegisterPartyListeners();
        }

        public void RegisterPartyListeners()
        {
            if (Campaign.Current == null)
            {
                Logger.Verbose("Unable to register party lifecycle listeners, no active campaign");
                return;
            }

            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, Handle_MobilePartyCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, Handle_MobilePartyDestroyed);
        }

        public void Handle_MobilePartyCreated(MobileParty party)
        {
            // not necessary - already exists in MBObjectManagerAdapater
            // mobilePartyRegistry.RegisterParty(party);

            if (party.IsAnyPlayerMainParty())
                return;

            messageBroker.Publish(this, new MobilePartyCreated(party));
        }

        public void Handle_MobilePartyDestroyed(MobileParty party, PartyBase partyBase)
        {
            mobilePartyRegistry.Remove(party);

            if (party.IsAnyPlayerMainParty()) 
                return;

            messageBroker.Publish(this, new MobilePartyDestroyed(party, partyBase));
        }
    }
}
