using Autofac;
using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Extensions;
using HarmonyLib;
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
        readonly Harmony harmony;
        public GuidRegistrationTests()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();

            _messageBroker = _container.Resolve<IMessageBroker>();

            harmony = new Harmony("com.Coop.GameInterface");
            harmony.PatchAll(typeof(GameInterface).Assembly);
        }


        [Fact]
        public void RegisterParties()
        {
            // Setup
            var objectManager = _container.Resolve<IObjectManager>();
            var parties = new MobileParty[NUM_PARTIES];

            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                party.StringId = $"Party {i}";

                objectManager.AddExisting(party.StringId, party);

                parties[i] = party;
            }

            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();

            // Execution
            partyRegistry.RegisterAllParties();

            // Verification
            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = parties[i];
                Assert.True(partyRegistry.TryGetValue(party, out string _));
            }
        }
    }
}
