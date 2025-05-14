using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core;
using Coop.Core.Client;
using Coop.Core.Server;
using GameInterface;
using Xunit;

namespace Coop.Tests.Autofac
{
    public class ContainerBuildTests
    {
        [Fact]
        public void Client_Container_Build()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ClientModule>();
            var container = builder.Build();

            ContainerProvider.SetContainer(container);

            Assert.NotNull(container);

            var client = container.Resolve<INetwork>();
            Assert.NotNull(client);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }

        [Fact]
        public void Server_Container_Build()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            var container = builder.Build();

            ContainerProvider.SetContainer(container);

            Assert.NotNull(container);

            var server = container.Resolve<INetwork>();
            Assert.NotNull(server);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }
    }
}
