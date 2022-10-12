using Autofac;
using Coop.Core.Server.Connections.States;

namespace Coop.Core.Server.Connections
{
    internal class ServerStatesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConnectionLogic>().As<IConnectionLogic>().SingleInstance();
            builder.RegisterType<PlayerConnectionStates>().As<IPlayerConnectionStates>().SingleInstance();
            builder.RegisterType<CoopServer>().As<ICoopServer>().As<ICoopNetwork>().As<ICoopNetwork>().SingleInstance();
            base.Load(builder);
        }
    }
}
