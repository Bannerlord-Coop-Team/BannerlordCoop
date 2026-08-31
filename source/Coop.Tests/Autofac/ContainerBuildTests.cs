using Autofac;
using Common.Commands;
using Common.LogicStates;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Common.Configuration;
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
            builder.RegisterModule<GameInterfaceModule>();
            using var container = builder.Build();

            Assert.NotNull(container);

            var client = container.Resolve<INetwork>();
            Assert.NotNull(client);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);

            var commandRegistry = container.Resolve<ICoopCommandRegistry>();
            Assert.True(commandRegistry.Contains("coop.debug.map_event.click_deployment_ready"));
            Assert.True(commandRegistry.Contains("coop.debug.battle.state"));
            Assert.True(commandRegistry.Contains("coop.debug.hero.id"));
            Assert.True(commandRegistry.Contains("coop.debug.campaign_options.list"));
            Assert.True(commandRegistry.Contains("coop.debug.player_captivity.capture_player"));
#if DEBUG
            Assert.True(commandRegistry.Contains("coop.debug.connection.join_state"));
            Assert.True(commandRegistry.Contains("coop.debug.connection.arm_inactive_party_deficit"));
            Assert.True(commandRegistry.Contains("coop.debug.connection.disconnect"));
            Assert.False(commandRegistry.Contains("coop.debug.connection.stage_inactive_party"));
#endif
        }

        [Fact]
        public void Server_Container_Build()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            using var container = builder.Build();

            Assert.NotNull(container);

            var server = container.Resolve<INetwork>();
            Assert.NotNull(server);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);

            var commandRegistry = container.Resolve<ICoopCommandRegistry>();
            Assert.True(commandRegistry.Contains("coop.debug.map_event.click_deployment_ready"));
            Assert.True(commandRegistry.Contains("coop.debug.battle.state"));
            Assert.True(commandRegistry.Contains("coop.debug.hero.id"));
            Assert.True(commandRegistry.Contains("coop.debug.campaign_options.list"));
            Assert.True(commandRegistry.Contains("coop.debug.player_captivity.capture_player"));
#if DEBUG
            Assert.True(commandRegistry.Contains("coop.debug.connection.join_state"));
            Assert.True(commandRegistry.Contains("coop.debug.connection.stage_inactive_party"));
            Assert.True(commandRegistry.Contains("coop.debug.connection.restore_inactive_party"));
            Assert.False(commandRegistry.Contains("coop.debug.connection.disconnect"));
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

            using var container = builder.Build();

            Assert.Same(selectedConfig, container.Resolve<SessionAdvertisementConfig>());
            Assert.Equal(visibility, container.Resolve<SessionAdvertisementConfig>().Visibility);
        }
    }
}
