using Autofac;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Save.Messages;
using System;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Save
{
    public class PackageObjectGuidsTests
    {
        readonly IContainer _container;
        readonly IMessageBroker _messageBroker;
        public PackageObjectGuidsTests()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterModule<GameInterfaceModule>();
            _container = builder.Build();

            _messageBroker = _container.Resolve<IMessageBroker>();
        }

        private void SetupRegistries(int numParties, int numHeroes, int numControlledHeroes)
        {
            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();
            var heroRegistry = _container.Resolve<IHeroRegistry>();
            var controlledHeros = _container.Resolve<IControlledHeroRegistry>();

            for (int i = 0; i < numParties; i++)
            {
                var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                party.StringId = $"Party {i}";

                partyRegistry.RegisterNewObject(party);
            }

            for (int i = 0; i < numHeroes; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                heroRegistry.RegisterNewObject(hero);
            }

            for (int i = numHeroes; i < numControlledHeroes + numHeroes; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                heroRegistry.RegisterNewObject(hero);

                // Ensure hero was registered
                Assert.True(heroRegistry.TryGetValue(hero, out Guid heroId));

                controlledHeros.RegisterAsControlled(heroId);
            }
        }

        [Fact]
        public void ResolveObjectGuids()
        {
            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            var partyRegistry = _container.Resolve<IMobilePartyRegistry>();
            var heroRegistry = _container.Resolve<IHeroRegistry>();
            var controlledHeros = _container.Resolve<IControlledHeroRegistry>();

            SetupRegistries(2, 2, 2);

            // Setup callback
            ObjectGuidsPackaged payload = default;
            _messageBroker.Subscribe<ObjectGuidsPackaged>((msg) =>
            {
                autoResetEvent.Set();
                payload = msg.What;
            });

            // Execution
            var transactionId = Guid.NewGuid();
            _messageBroker.Publish(this, new PackageObjectGuids(transactionId));

            // Verification
            // Wait for callback with 1 sec timeout
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(1)));

            Assert.Equal(transactionId, payload.TransactionID);

            Assert.Equal(partyRegistry.Count, payload.PartyIds.Count);
            Assert.Equal(heroRegistry.Count, payload.HeroIds.Count);
            Assert.Equal(controlledHeros.ControlledHeros.Count, payload.ControlledHeros.Count);
        }
    }
}
