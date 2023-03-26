﻿using Autofac;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using System.IO.Ports;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;

namespace GameInterface.Tests.Services.Heroes
{
    public class RetrieveHeroAssociationsTests
    {
        // Number of heroes to create for each test
        // Must be greater than 0
        private const int NUM_HEROES = 2;

        readonly IContainer _container;
        readonly IMessageBroker _messageBroker;
        public RetrieveHeroAssociationsTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();

            _messageBroker = _container.Resolve<IMessageBroker>();
        }

        [Fact]
        public void RegisterHeroes()
        {
            // Setup
            GameBootStrap.SetupCampaignObjectManager();

            var autoResetEvent = new AutoResetEvent(false);
            var heroRegistry = _container.Resolve<IHeroRegistry>();
            var heroes = new Hero[NUM_HEROES];

            for (int i = 0; i < NUM_HEROES; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                Campaign.Current.CampaignObjectManager.AddHero(hero);

                heroRegistry.RegisterNewObject(hero);

                heroes[i] = hero;
            }
            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();

            // Setup Callback
            _messageBroker.Subscribe<PartiesRegistered>((payload) =>
            {
                autoResetEvent.Set();
            });

            // Execution
            Guid transactionId = Guid.NewGuid();
            _messageBroker.Publish(this, new RegisterAllParties());

            // Verification
            // Wait for callback with 1 sec timeout
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));

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

                heroRegistry.RegisterNewObject(hero);

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
