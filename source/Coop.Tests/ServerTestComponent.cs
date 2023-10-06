using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core;
using Coop.Core.Common;
using Coop.Core.Server;
using Coop.Tests.Mocks;
using Xunit.Abstractions;

namespace Coop.Tests
{
    internal class ServerTestComponent
    {
        public MockMessageBroker MockMessageBroker { get; }
        public MockNetwork MockNetwork { get; }
        public ITestOutputHelper Output { get; }
        public IContainer Container { get; }

        public ServerTestComponent(ITestOutputHelper output)
        {
            Output = output;

            var containerProvider = new ContainerProvider();
            var builder = new ContainerBuilder();
            builder.RegisterModule<CommonModule>();
            builder.RegisterModule<ServerModule>();
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
