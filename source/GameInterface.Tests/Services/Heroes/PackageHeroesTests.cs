using Autofac;
using Common.Messaging;
using GameInterface.Services.Heroes;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes
{
    public class RetrieveHeroAssociationsTests
    {
        // Number of heroes to create for each test
        // Must be greater than 0
        private const int NUM_HEROES = 2;

        readonly IContainer _container;
        public RetrieveHeroAssociationsTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();
        }

        [Fact]
        public void RegisterHeroes()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

            var heroRegistry = _container.Resolve<IHeroRegistry>();
            var heroes = new Hero[NUM_HEROES];

            for (int i = 0; i < NUM_HEROES; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                Campaign.Current.CampaignObjectManager.AddHero(hero);

                heroes[i] = hero;
            }

            // Execution
            heroRegistry.RegisterAllHeroes();

            // Verification
            foreach (var hero in heroes)
            {
                Assert.True(heroRegistry.TryGetValue(hero, out Guid _));
            }
        }

        [Fact]
        public void RegisterHeroesFromAssociations()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

            var heroRegistry = _container.Resolve<IHeroRegistry>();
            var heroGuids = new (Hero, Guid)[NUM_HEROES];
            var heroAssociations = new Dictionary<string, Guid>();

            for (int i = 0; i < NUM_HEROES; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                Campaign.Current.CampaignObjectManager.AddHero(hero);

                var heroId = Guid.NewGuid();

                heroGuids[i] = (hero, heroId);
                heroAssociations.Add(hero.StringId, heroId);
            }

            // Execution
            heroRegistry.RegisterHeroesWithStringIds(heroAssociations);

            // Verification
            // Wait for callback with 1 sec timeout
            Assert.NotEmpty(heroGuids);

            for (int i = 0; i < NUM_HEROES; i++)
            {
                var hero = heroGuids[i].Item1;
                var heroId = heroGuids[i].Item2;

                Assert.True(heroRegistry.TryGetValue(hero, out Guid resolvedGuid));
                Assert.Equal(heroId, resolvedGuid);
            }
        }
    }
}
