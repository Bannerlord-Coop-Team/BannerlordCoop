using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core.Common;
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
            builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().InstancePerLifetimeScope();
            builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();

            foreach(var handlerType in HandlerCollector.Collect<ClientModule>())
            {
                builder.RegisterType(handlerType).AsSelf().InstancePerLifetimeScope().AutoActivate();
            }
            base.Load(builder);
        }
    }
}
