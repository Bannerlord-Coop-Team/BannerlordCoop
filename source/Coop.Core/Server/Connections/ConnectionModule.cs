using Autofac;
using Coop.Core.Common;

namespace Coop.Core.Server.Connections;

internal class ConnectionModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        foreach (var handlerType in TypeCollector.Collect<ConnectionModule, IConnectionState>())
        {
            builder.RegisterType(handlerType).AsSelf();
        }

        builder.RegisterType<ConnectionLogicFactory>().As<IConnectionLogicFactory>().InstancePerLifetimeScope();
        builder.RegisterType<ConnectionLogic>().As<IConnectionLogic>();

        builder.RegisterType<ClientRegistry>().As<IClientRegistry>().AsSelf().InstancePerLifetimeScope().AutoActivate();

        base.Load(builder);
    }
}
