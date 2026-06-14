using Autofac;

namespace Coop.Core.Server.Connections;

internal class ConnectionModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Connection states are constructed by ConnectionLogic from the ConnectionContext, so they
        // no longer need to be resolved from the container individually.
        builder.RegisterType<ConnectionContext>().AsSelf().InstancePerLifetimeScope();

        // ClientRegistry creates connection logics directly via the context; the registration is kept
        // so they can also be resolved standalone (e.g. in tests) with a playerId parameter.
        builder.RegisterType<ConnectionLogic>().As<IConnectionLogic>().AsSelf();

        builder.RegisterType<ClientRegistry>().As<IConnectionCollection>().AsSelf().InstancePerLifetimeScope().AutoActivate();

        base.Load(builder);
    }
}
