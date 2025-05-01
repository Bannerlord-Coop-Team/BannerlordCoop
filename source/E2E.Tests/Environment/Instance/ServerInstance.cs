using Autofac;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server;
using E2E.Tests.Environment.Mock;

namespace E2E.Tests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ServerInstance : EnvironmentInstance
{
    public override ILifetimeScope Container { get; }

    protected override TestMessageBroker MessageBroker { get; }
    protected override MockNetworkBase MockNetwork { get; }

    public ServerInstance()
    {
        var builder = new ContainerBuilder();

        builder.RegisterModule<ServerModule>();
        builder.RegisterType<MockServer>().AsSelf().As<INetwork>().As<ICoopServer>().InstancePerLifetimeScope();
        builder.RegisterType<ServerInstance>().AsSelf();

        AddSharedDependencies(builder);

        Container = builder.Build();

        MessageBroker = Container.Resolve<TestMessageBroker>();
        MockNetwork = Container.Resolve<MockNetworkBase>();
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
