using Autofac;
using Coop.Mod;
using Coop.Mod.LogicStates.Server;
using Coop.Mod.Server;
using Coop.Mod.Server.States;
using LiteNetLib;

namespace Coop
{
    internal class ServerModule : CoopModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServerLogic>().As<IServerLogic>().SingleInstance();
            builder.RegisterType<CoopServer>().As<ICoopServer>().As<ICoopNetwork>().As<INetEventListener>().SingleInstance();
            builder.RegisterType<InitialServerState>().As<IServerState>();
            base.Load(builder);
        }
    }
}