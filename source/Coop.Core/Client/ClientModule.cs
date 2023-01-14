using Autofac;
using Coop.Core.Client.States;
using Coop.Core.Communication.PacketHandlers;
using LiteNetLib;

namespace Coop.Core.Client
{
    /// <summary>
    /// Client state DI container
    /// </summary>
    internal class ClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<PacketManager>().As<IPacketManager>();
            builder.RegisterType<ClientLogic>().As<IClientLogic>().SingleInstance();
            builder.RegisterType<CoopClient>().As<ICoopClient>().As<ICoopNetwork>().As<INetEventListener>().SingleInstance();
            base.Load(builder);
        }
    }
}
