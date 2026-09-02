using Autofac;
using Common.Commands;
using Common.LogicStates;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Common.Configuration;
using Coop.Core.Client;
using Coop.Core.Server;
using Coop.Core.Server.Services.Telemetry;
using Coop.Tests.Mocks;
using GameInterface;
using System.Collections.Generic;
using System.Linq;
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
            builder.RegisterModule<GameInterfaceModule>();
            using var container = builder.Build();

            Assert.NotNull(container);

            var client = container.Resolve<INetwork>();
            Assert.NotNull(client);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);

            ICoopCommand[] commands = container.Resolve<IEnumerable<ICoopCommand>>().ToArray();
            Assert.Contains(commands, command =>
                $"{command.Prefix}.{command.Name}" == "coop.debug.workshop.set_workshop_custom_name");
        }

        [Fact]
        public void Server_Container_Build()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            RegisterMockServerTelemetry(builder);
            using var container = builder.Build();

            Assert.NotNull(container);

            var server = container.Resolve<INetwork>();
            Assert.NotNull(server);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }

        [Theory]
        [InlineData(ServerVisibility.FriendsOnly)]
        [InlineData(ServerVisibility.None)]
        public void Server_Container_UsesHostSelectedAdvertisementConfig(ServerVisibility visibility)
        {
            var selectedConfig = new SessionAdvertisementConfig { Visibility = visibility };
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            builder.RegisterInstance(selectedConfig).AsSelf().SingleInstance();
            RegisterMockServerTelemetry(builder);

            using var container = builder.Build();

            Assert.Same(selectedConfig, container.Resolve<SessionAdvertisementConfig>());
            Assert.Equal(visibility, container.Resolve<SessionAdvertisementConfig>().Visibility);
        }

        private static void RegisterMockServerTelemetry(ContainerBuilder builder)
        {
            builder.RegisterType<MockServerTelemetryUploader>()
                .As<IServerTelemetryUploader>()
                .As<IBattlesFoughtUploader>()
                .SingleInstance();
        }
    }
}
