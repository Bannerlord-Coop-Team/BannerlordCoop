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
using GameInterface.Services.ObjectManager;

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
            GameBootStrap.Initialize();

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
            var objectManager = _container.Resolve<IObjectManager>();
            var heroes = CreateHeroes(objectManager, NUM_HEROES);

            var controlledHeroes = new HashSet<string>();

            for (int i = 0; i < NUM_HEROES / 2; i++)
            {
                controlledHeroes.Add(heroes[i].StringId);
            }

            AutoResetEvent autoResetEvent = new AutoResetEvent(false);
            _messageBroker.Subscribe<ExistingObjectGuidsLoaded>((payload) =>
            {
                autoResetEvent.Set();
            });

            // Execution
            var transactionId = Guid.NewGuid();

            var objectGuids = new GameObjectGuids(
                controlledHeroes.ToArray());

            var message = new LoadExistingObjectGuids(
                transactionId,
                objectGuids);

            _messageBroker.Publish(this, message);

            // Verification
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));

            foreach (var hero in heroes)
            {
                var heroId = hero.StringId;
                Assert.True(objectManager.TryGetObject(heroId, out Hero resolvedHero));

                Assert.Equal(hero, resolvedHero);
            }
        }

        private Hero[] CreateHeroes(IObjectManager objectManager, int numHeroes)
        {
            var heroes = new Hero[numHeroes];

            Type type = typeof(Hero);

            for (int i = 0; i < numHeroes; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(type);
                hero.StringId = $"Hero {i}";
                objectManager.AddExisting(hero.StringId, hero);

                heroes[i] = hero;
            }

            return heroes;
        }
    }
}
