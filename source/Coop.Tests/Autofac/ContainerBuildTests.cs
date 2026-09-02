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
using Missions;
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

            ICoopCommand[] registeredCommands = container.Resolve<IEnumerable<ICoopCommand>>().ToArray();
            Assert.Contains(registeredCommands, command =>
                $"{command.Prefix}.{command.Name}" == "coop.debug.workshop.set_workshop_custom_name");
            Assert.Contains(registeredCommands, command =>
                $"{command.Prefix}.{command.Name}" == "coop.debug.map_event.kms");

            ICoopCommand[] missionCommands = registeredCommands
                .Where(command => command.GetType().Assembly == typeof(MissionModule).Assembly)
                .ToArray();
#if DEBUG
            Assert.Equal(26, missionCommands.Length);
            Assert.Equal(
                new[] { "arm_inactive_party_deficit", "disconnect", "join_state" },
                registeredCommands
                    .Where(command => command.Prefix == "coop.debug.connection")
                    .Select(command => command.Name)
                    .OrderBy(name => name));
#else
            Assert.Equal(15, missionCommands.Length);
#endif
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

#if DEBUG
            ICoopCommand[] registeredCommands = container.Resolve<IEnumerable<ICoopCommand>>().ToArray();
            Assert.Equal(
                new[] { "join_state", "restore_inactive_party", "stage_inactive_party" },
                registeredCommands
                    .Where(command => command.Prefix == "coop.debug.connection")
                    .Select(command => command.Name)
                    .OrderBy(name => name));
#endif
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
