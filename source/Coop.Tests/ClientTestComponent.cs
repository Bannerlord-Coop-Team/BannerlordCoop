using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core;
using Coop.Core.Client;
using Coop.Tests.Mocks;
using Xunit.Abstractions;

namespace Coop.Tests
{
    internal class ClientTestComponent
    {
        public MockMessageBroker MockMessageBroker { get; }
        public MockNetwork MockNetwork { get; }
        public ITestOutputHelper Output { get; }
        public IContainer Container { get; }

        public ClientTestComponent(ITestOutputHelper output)
        {
            Output = output;

            var containerProvider = new ContainerProvider();
            var builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ClientModule>();
            builder.RegisterType<MockMessageBroker>().AsSelf().As<IMessageBroker>().InstancePerLifetimeScope();
            builder.RegisterType<MockNetwork>().AsSelf().As<INetwork>().InstancePerLifetimeScope();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>();


            Container = builder.Build();

            containerProvider.SetProvider(Container);

            MockMessageBroker = Container.Resolve<MockMessageBroker>()!;
            MockNetwork = Container.Resolve<MockNetwork>()!;
        }
    }
}
