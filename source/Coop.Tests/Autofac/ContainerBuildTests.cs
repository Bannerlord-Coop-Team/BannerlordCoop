using Autofac;
using Common.LogicStates;
using Coop.Core;
using Coop.Core.Client;
using Coop.Core.Server;
using Xunit;

namespace Coop.Tests.Autofac
{
    public class ContainerBuildTests
    {
        [Fact]
        public void Client_Container_Build()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ClientModule>();
            var container = builder.Build();

            Assert.NotNull(container);

            var client = container.Resolve<ICoopNetwork>();
            Assert.NotNull(client);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }

        [Fact]
        public void Server_Container_Build()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ServerModule>();
            var container = builder.Build();

            Assert.NotNull(container);

            var server = container.Resolve<ICoopNetwork>();
            Assert.NotNull(server);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }
    }
}
