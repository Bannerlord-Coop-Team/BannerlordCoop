using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface;
using GameInterface.Missions.Arena;
using GameInterface.Missions.Services.Network;
using IntroServer.Config;
using LiteNetLib;
using Missions;
using Missions.Services;
using Missions.Services.Network;
using System;
using Xunit;

namespace MissionTests
{
    public class AutoFacTests
    {
        IContainer _container;
        public AutoFacTests()
        {
            ContainerBuilder builder = new ContainerBuilder();

            // MissionModule is no longer self-contained — it expects the base services (serializer,
            // type mapper, packet manager, message broker) and GameInterfaceModule that the Coop.Core
            // client container provides in production. Compose them here so the standalone resolve works.
            builder.RegisterModule<GameInterfaceModule>();
            // AsSelf for the Missions P2P client + As<INetworkConfiguration> to stand in for the campaign
            // config (in production that interface comes from the Coop.Core client container).
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<ProtoBufSerializer>().As<ICommonSerializer>().InstancePerLifetimeScope();
            builder.RegisterType<SerializableTypeMapper>().As<ISerializableTypeMapper>().InstancePerLifetimeScope();
            builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
            builder.RegisterInstance(MessageBroker.Instance)
                .As<IMessageBroker>()
                .SingleInstance()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            // INetwork is CoopClient in production (ClientModule), which this standalone container does not
            // compose. GameInterfaceModule auto-activates AutoRegistryFactory, which requires an INetwork,
            // so register a no-op stub. LiteNetP2PClient is intentionally NOT used here — it is only an
            // IMissionNetwork, never an INetwork.
            builder.RegisterType<StubNetwork>().As<INetwork>().SingleInstance();

            builder.RegisterModule<MissionModule>();

            _container = builder.Build();
        }

        [Fact]
        public void MissionModule_Resolve_CoopMissionNetworkBehavior()
        {
            var networkBehavior = _container.Resolve<CoopMissionNetworkBehavior>();

            Assert.NotNull(networkBehavior);
        }

        [Fact]
        public void MissionModule_Resolve_CoopArenaController()
        {
            var networkBehavior = _container.Resolve<CoopArenaController>();

            Assert.NotNull(networkBehavior);
        }

        /// <summary>
        /// Stand-in for the production INetwork (CoopClient) so GameInterfaceModule's startables activate.
        /// Never invoked by these resolve-only tests; all members throw if called.
        /// </summary>
        private sealed class StubNetwork : INetwork
        {
            public INetworkConfiguration Configuration => throw new NotImplementedException();
            public void Send(NetPeer netPeer, IPacket packet) => throw new NotImplementedException();
            public void SendImmediate(NetPeer netPeer, IPacket packet) => throw new NotImplementedException();
            public void SendAll(IPacket packet) => throw new NotImplementedException();
            public void SendAllBut(NetPeer excludedPeer, IPacket packet) => throw new NotImplementedException();
            public void Send(NetPeer netPeer, IMessage message) => throw new NotImplementedException();
            public void SendImmediate(NetPeer netPeer, IMessage message) => throw new NotImplementedException();
            public void SendAll(IMessage message) => throw new NotImplementedException();
            public void SendAllBut(NetPeer excludedPeer, IMessage message) => throw new NotImplementedException();
            public void Start() => throw new NotImplementedException();
            public void Dispose() { }
        }
    }
}