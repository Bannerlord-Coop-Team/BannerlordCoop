using Autofac;
using Common.Messaging;
using Coop.Core.Communication.PacketHandlers;
using Coop.Core.Configuration;
using Coop.Core.Debugging.Logger;
using Coop.Core.Server.Connections;
using GameInterface;

namespace Coop.Core
{
    internal class CoopModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            #region Logging
            builder.RegisterType<NLogLogger>().As<ILogger>().SingleInstance();
            #endregion

            #region Network
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().OwnedByLifetimeScope();
            #endregion

            #region Communication
            builder.RegisterType<PacketManager>().As<IPacketManager>().SingleInstance();
            builder.RegisterInstance(MessageBroker.Instance).As<IMessageBroker>().SingleInstance();
            builder.RegisterType<Connection>().As<IConnection>();
            #endregion

            #region GameInterface
            builder.RegisterModule<GameInterfaceModule>();
            #endregion

            base.Load(builder);
        }
    }
}
