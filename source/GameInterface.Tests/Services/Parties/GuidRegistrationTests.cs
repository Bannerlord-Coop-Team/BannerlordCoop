using Autofac;
using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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

            var parties = new MobileParty[NUM_PARTIES];

            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                party.StringId = $"Hero {i}";

                Campaign.Current.CampaignObjectManager.AddMobileParty(party);

                parties[i] = party;
            }

            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();

            // Execution
            partyRegistry.RegisterAllParties();

            // Verification
            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = parties[i];
                Assert.True(partyRegistry.TryGetValue(party, out Guid _));
            }
        }

        [Fact]
        public void RegisterPartiesFromAssociations()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

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

            // Execution
            partyRegistry.RegisterPartiesWithStringIds(partyAssociations);

            // Verification
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
