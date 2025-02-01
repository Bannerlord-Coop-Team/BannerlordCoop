using Autofac;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
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
            // TODO Not transiant so it break with other tests
            var objectManager = _container.Resolve<IObjectManager>();

            for (int i = numHeroes; i < numControlledHeroes + numHeroes; i++)
            {
                var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                hero.StringId = $"Hero {i}";

                objectManager.AddExisting(hero.StringId, hero);

                // Ensure hero was registered
                Assert.True(objectManager.TryGetObject(hero.StringId, out Hero _));
            }
        }
    }
}
