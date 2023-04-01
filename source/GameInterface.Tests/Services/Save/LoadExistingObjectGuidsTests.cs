using Autofac;
using Common.Messaging;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Xunit;
using GameInterface.Tests.Bootstrap.Extensions;
using GameInterface.Services.Save.Messages;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Heroes;
using GameInterface.Services.Save.Data;
using System.Linq;

namespace GameInterface.Tests.Services.Save
{
    public class LoadExistingObjectGuidsTests
    {
        private const int NUM_PARTIES = 2;
        private const int NUM_HEROES = 2;

        readonly IContainer _container;
        readonly IMessageBroker _messageBroker;
        public LoadExistingObjectGuidsTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();

            _messageBroker = _container.Resolve<IMessageBroker>();
        }

        [Fact]
        public void LoadExistingObjectGuids_SendReceive()
        {
            // Setup
            var partyGuids = CreateMobileParties(NUM_PARTIES);
            var partyAssociations = new Dictionary<string, Guid>();

            for (int i = 0; i < NUM_PARTIES; i++)
            {
                var party = partyGuids[i].Item1;
                partyAssociations.Add(party.StringId, partyGuids[i].Item2);
            }

            var heroGuids = CreateHeroes(NUM_HEROES);
            var heroAssociations = new Dictionary<string, Guid>();

            for (int i = 0; i < NUM_HEROES; i++)
            {
                var hero = heroGuids[i].Item1;
                heroAssociations.Add(hero.StringId, heroGuids[i].Item2);
            }

            var controlledHeroes = new HashSet<Guid>();

            for (int i = 0; i < NUM_HEROES / 2; i++)
            {
                controlledHeroes.Add(heroGuids[i].Item2);
            }

            AutoResetEvent autoResetEvent = new AutoResetEvent(false);
            _messageBroker.Subscribe<ExistingObjectGuidsLoaded>((payload) =>
            {
                autoResetEvent.Set();
            });

            // Execution
            var transactionId = Guid.NewGuid();

            var objectGuids = new GameObjectGuids(
                controlledHeroes.ToArray(),
                partyAssociations,
                heroAssociations);

            var message = new LoadExistingObjectGuids(
                transactionId,
                objectGuids);

            _messageBroker.Publish(this, message);

            // Verification
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));

            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();

            foreach(var partyGuid in partyGuids)
            {
                var partyId = partyGuid.Item2;
                Assert.True(partyRegistry.TryGetValue(partyId, out MobileParty resolvedParty));

                var party = partyGuid.Item1;
                Assert.Equal(party, resolvedParty);
            }

            var heroRegistry = _container.Resolve<IHeroRegistry>();

            foreach (var heroGuid in heroGuids)
            {
                var heroId = heroGuid.Item2;
                Assert.True(heroRegistry.TryGetValue(heroId, out Hero resolvedHero));

                var hero = heroGuid.Item1;
                Assert.Equal(hero, resolvedHero);
            }
        }

        private (MobileParty, Guid)[] CreateMobileParties(int numParties)
        {
            GameBootStrap.SetupCampaignObjectManager();

            var partyGuids = new (MobileParty, Guid)[numParties];

            Type type = typeof(MobileParty);

            for (int i = 0; i < numParties; i++)
            {
                var partyId = Guid.NewGuid();
                var party = (MobileParty)FormatterServices.GetUninitializedObject(type);
                party.StringId = $"Party {i}";
                Campaign.Current.CampaignObjectManager.AddMobileParty(party);
                partyGuids[i] = (party, partyId);
            }

            return partyGuids;
        }

        private (Hero, Guid)[] CreateHeroes(int numHeroes)
        {
            GameBootStrap.SetupCampaignObjectManager();

            var heroGuids = new (Hero, Guid)[numHeroes];

            Type type = typeof(Hero);

            for (int i = 0; i < numHeroes; i++)
            {
                var heroId = Guid.NewGuid();
                var hero = (Hero)FormatterServices.GetUninitializedObject(type);
                hero.StringId = $"Party {i}";
                Campaign.Current.CampaignObjectManager.AddHero(hero);

                heroGuids[i] = (hero, heroId);
            }

            return heroGuids;
        }
    }
}
