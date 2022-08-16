using Autofac;
using Autofac.Builder;
using Common.Messages;
using Coop.Communication;
using Coop.Communication.PacketHandlers;
using Coop.Configuration;
using Coop.Debugging.Logger;
using Coop.Mod.Server.Connections;
using Coop.Serialization;
using GameInterface.Serialization;
using LiteNetLib;

namespace Coop
{
    internal abstract class CoopModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            #region Logging
            builder.RegisterType<NLogLogger>().As<ILogger>().SingleInstance();
            #endregion

            #region Network
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().OwnedByLifetimeScope();
            builder.RegisterType<NetManager>().AsSelf().SingleInstance();
            #endregion

            #region Communication
            builder.RegisterType<ProtobufSerializer>().As<ISerializer>().SingleInstance();
            builder.RegisterType<PacketManager>().As<IPacketManager>().SingleInstance();
            builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
            builder.RegisterType<Connection>().As<IConnection>();
            #endregion

            #region GameInterface
            builder.RegisterType<GameInterface.GameInterface>().AsSelf().SingleInstance();
            #endregion

            base.Load(builder);
        }
    }
}
