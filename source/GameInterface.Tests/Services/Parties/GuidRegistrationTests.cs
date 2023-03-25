using Autofac;
using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Extensions;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Parties
{
    public class GuidRegistrationTests
    {
        // Number of parties to create for each test
        // Must be greater than 0
        private const int NUM_PARTIES = 2;

        readonly IContainer _container;
        readonly IMessageBroker _messageBroker;
        public GuidRegistrationTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();

            _messageBroker = _container.Resolve<IMessageBroker>();
        }


        [Fact]
        public void RegisterParties()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            Guid transactionId = Guid.NewGuid();

            var party1 = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            var party2 = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            party1.StringId = "Party 1";
            party2.StringId = "Party 2";

            Campaign.Current.CampaignObjectManager.AddMobileParty(party1);
            Campaign.Current.CampaignObjectManager.AddMobileParty(party2);

            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();

            // Setup Callback
            _messageBroker.Subscribe<PartiesRegistered>((payload) =>
            {
                autoResetEvent.Set();
            });

            // Execution
            _messageBroker.Publish(this, new RegisterParties());

            // Verification
            // Wait for callback with 1 sec timeout
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));

            Assert.True(partyRegistry.TryGetValue(party1, out Guid _));
            Assert.True(partyRegistry.TryGetValue(party2, out Guid _));
        }

        [Fact]
        public void RegisterPartiesFromAssociations()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();

            

            var partyGuids = new (MobileParty, Guid)[NUM_PARTIES];
            var partyAssociations = new Dictionary<string, Guid>();

            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                party.StringId = $"Party {i}";

                Campaign.Current.CampaignObjectManager.AddMobileParty(party);

                var partyId = Guid.NewGuid();

                partyGuids[i] = (party, partyId);
                partyAssociations.Add(party.StringId, partyId);
            }

            // Setup Callback
            PartiesRegistered partiesRegistered = default;
            _messageBroker.Subscribe<PartiesRegistered>((payload) =>
            {
                partiesRegistered = payload.What;
                autoResetEvent.Set();
            });

            // Execution
            Guid transactionId = Guid.NewGuid();
            _messageBroker.Publish(this, new RegisterPartiesWithStringIds(transactionId, partyAssociations));

            // Verification
            // Wait for callback with 1 sec timeout
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));

            Assert.Equal(transactionId, partiesRegistered.TransactionID);
            Assert.NotEmpty(partyGuids);

            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = partyGuids[i].Item1;
                var partyId = partyGuids[i].Item2;

                Assert.True(partyRegistry.TryGetValue(party, out Guid resolvedGuid));
                Assert.Equal(partyId, resolvedGuid);
            }
        }
    }
}
