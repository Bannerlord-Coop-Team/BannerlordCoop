using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.States;
using LiteNetLib;

namespace Coop.Core.Server
{
    /// <summary>
    /// Server dependencies
    /// </summary>
    internal class ServerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServerLogic>().As<IServerLogic>().As<ILogic>().SingleInstance();
            builder.RegisterType<CoopServer>().As<ICoopServer>().As<INetwork>().As<INetEventListener>().SingleInstance();
            builder.RegisterType<InitialServerState>().As<IServerState>();
            builder.RegisterType<ClientRegistry>().As<IClientRegistry>().SingleInstance();
            base.Load(builder);
        }
    }
}