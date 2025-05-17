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

    public ServerInstance(TestNetworkRouter networkOrchestrator)
    {
        var builder = new ContainerBuilder();

        builder.RegisterModule<ServerModule>();
        builder.RegisterType<MockServer>().AsSelf().As<MockNetworkBase>().As<INetwork>().As<ICoopServer>().InstancePerLifetimeScope();
        builder.RegisterType<ServerInstance>().AsSelf();

        AddSharedDependencies(builder, networkOrchestrator, registerGameInterface: true);

        Container = builder.Build();

        MessageBroker = Container.Resolve<TestMessageBroker>();
        MockNetwork = Container.Resolve<MockNetworkBase>();

        networkOrchestrator.AddServer(this);
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
