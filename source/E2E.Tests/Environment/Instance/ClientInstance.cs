using Autofac;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core;
using Coop.Core.Client;
using E2E.Tests.Environment.Mock;

namespace E2E.Tests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ClientInstance : EnvironmentInstance
{
    protected override TestMessageBroker MessageBroker { get; }
    protected override MockNetworkBase MockNetwork { get; }

    public override ILifetimeScope Container { get; }

    public ClientInstance(TestNetworkRouter networkOrchestrator)
    {
        var builder = new ContainerBuilder();

        builder.RegisterModule<ClientModule>();
        builder.RegisterType<MockClient>().AsSelf().As<MockNetworkBase>().As<INetwork>().As<ICoopClient>().InstancePerLifetimeScope();
        builder.RegisterType<ClientInstance>().AsSelf();

        AddSharedDependencies(builder, networkOrchestrator, registerGameInterface: true);

        Container = builder.Build();

        MessageBroker = Container.Resolve<TestMessageBroker>();
        MockNetwork = Container.Resolve<MockNetworkBase>();

        networkOrchestrator.AddClient(this);
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
