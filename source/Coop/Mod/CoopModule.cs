using Autofac;
using Coop.Communication;
using Coop.Communication.MessageBroker;
using Coop.Communication.PacketHandlers;
using Coop.Configuration;
using Coop.Debugging.Logger;
using Coop.Serialization;
using LiteNetLib;

namespace Coop
{
    internal abstract class CoopModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<NLogLogger>().As<ILogger>().SingleInstance();

            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().OwnedByLifetimeScope();
            builder.RegisterType<NetManager>().AsSelf().SingleInstance();

            builder.RegisterType<ProtobufSerializer>().As<ISerializer>().SingleInstance();
            builder.RegisterType<PacketManager>().As<IPacketManager>().SingleInstance();
            builder.RegisterType<NetworkMessageBroker>().As<IMessageBroker>().SingleInstance();

            builder.RegisterType<GameInterface.GameInterface>().AsSelf().SingleInstance();

            base.Load(builder);
        }
    }
}
