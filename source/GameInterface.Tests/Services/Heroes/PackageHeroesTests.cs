using Autofac;
using Common.Messaging;
using GameInterface.Services.Registry;
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
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();
        }

        [Fact]
        public void RegisterHeroes()
        {
            // Setup
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
                Assert.True(heroRegistry.TryGetId(hero, out string _));
            }
        }
    }
}
