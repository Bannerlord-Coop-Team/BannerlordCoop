using Autofac;
using Common.LogicStates;
using Coop.Mod;
using Coop.Mod.LogicStates.Client;
using LiteNetLib;

namespace Coop.Mod.Client
{
    internal class ClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ClientLogic>().As<IClientLogic>();
            builder.RegisterType<CoopClient>().As<ICoopClient>().As<ICoopNetwork>().As<INetEventListener>().SingleInstance();
            builder.RegisterType<InitialClientState>().As<IState>();
            base.Load(builder);
        }
    }
}
