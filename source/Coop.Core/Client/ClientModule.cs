using Autofac;
using Common.LogicStates;
using Coop.Core;
using Coop.Core.Client.States;
using LiteNetLib;

namespace Coop.Core.Client
{
    internal class ClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ClientLogic>().As<ILogic>();
            builder.RegisterType<CoopClient>().As<ICoopClient>().As<ICoopNetwork>().As<INetEventListener>().SingleInstance();
            builder.RegisterType<MainMenuState>().As<IState>();
            base.Load(builder);
        }
    }
}
