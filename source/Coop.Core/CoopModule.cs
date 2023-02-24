using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Configuration;
using Coop.Core.Server.Connections;
using GameInterface;

namespace Coop.Core
{
    internal class CoopModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            #region Network
            builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().OwnedByLifetimeScope();
            #endregion

            #region Communication
            builder.RegisterType<PacketManager>().As<IPacketManager>().SingleInstance();
            builder.RegisterType<EventPacketHandler>().AsSelf().SingleInstance().AutoActivate();
            builder.RegisterType<NetworkMessageBroker>().As<INetworkMessageBroker>().As<IMessageBroker>()
                .SingleInstance()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            #endregion

            #region GameInterface
            builder.RegisterModule<GameInterfaceModule>();
            #endregion

            base.Load(builder);
        }
    }
}
