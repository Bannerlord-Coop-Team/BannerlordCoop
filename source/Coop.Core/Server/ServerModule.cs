using Autofac;
using Common.LogicStates;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.States;
using LiteNetLib;

namespace Coop.Core.Server
{
    internal class ServerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServerLogic>().As<ILogic>().SingleInstance();
            builder.RegisterType<CoopServer>().As<ICoopServer>().As<ICoopNetwork>().As<INetEventListener>().SingleInstance();
            builder.RegisterType<InitialServerState>().As<IServerState>();
            builder.RegisterType<PlayerConnectionStatesManager>().As<IPlayerConnectionStatesManager>().SingleInstance();
            builder.RegisterType<ClientStateOrchestrator>().As<IClientStateOrchestrator>().SingleInstance();
            base.Load(builder);
        }
    }
}