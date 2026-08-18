using Autofac;
using Common.LogicStates;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Common.Configuration;
using Coop.Core.Client;
using Coop.Core.Server;
using GameInterface;
using Moq;
using System.Net;
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
        }

        [Fact]
        public void ServerRuntime_IdentityBridgePrecedesProviderResolver()
        {
            string bridgeName = PeerIdentityBridgeName.Create();
            var hostEndpoint = new IPEndPoint(IPAddress.Loopback, 43143);
            var providerEndpoint = new IPEndPoint(IPAddress.Loopback, 43144);
            var hostIdentity = new PlatformIdentity("steam", "42");
            var providerIdentity = new PlatformIdentity("gog", "84");
            var providerResolver = new Mock<IAuthenticatedPeerIdentityResolver>();
            providerResolver
                .Setup(resolver => resolver.TryGetIdentity(providerEndpoint, out providerIdentity))
                .Returns(true);
            var providerRuntime = new Mock<ISessionProviderRuntime>();
            providerRuntime.SetupGet(runtime => runtime.PeerIdentityResolver)
                .Returns(providerResolver.Object);
            var runtime = ClientModule.AddPeerIdentityBridge(providerRuntime.Object, bridgeName);

            using (var publisher = new NamedPipePeerIdentityPublisher(bridgeName))
            {
                Assert.True(publisher.TryRegister(hostEndpoint, hostIdentity));
                Assert.True(runtime.PeerIdentityResolver.TryGetIdentity(hostEndpoint, out var resolvedHost));
                Assert.Equal(hostIdentity, resolvedHost);
                providerResolver.Verify(
                    resolver => resolver.TryGetIdentity(
                        hostEndpoint,
                        out It.Ref<PlatformIdentity>.IsAny),
                    Times.Never);

                Assert.True(runtime.PeerIdentityResolver.TryGetIdentity(
                    providerEndpoint,
                    out var resolvedProvider));
                Assert.Equal(providerIdentity, resolvedProvider);
            }

            runtime.Dispose();
            runtime.Dispose();
            providerRuntime.Verify(inner => inner.Dispose(), Times.Once);
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
