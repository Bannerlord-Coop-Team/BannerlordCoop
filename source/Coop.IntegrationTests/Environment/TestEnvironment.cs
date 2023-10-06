using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core;
using Coop.Core.Client;
using Coop.Core.Common;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Save;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment.Mock;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Microsoft.Extensions.DependencyInjection;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Environment used for integration testing
/// </summary>
internal class TestEnvironment
{
    /// <summary>
    /// Constructor for TestEnvironment
    /// </summary>
    /// <param name="numClients">Number of clients to create, defaults to 2 clients</param>
    public TestEnvironment(int numClients = 2)
    {
        Server = CreateServer();

        var clients = new EnvironmentInstance[numClients];
        for (int i = 0; i < numClients; i++)
        {
            clients[i] = CreateClient();
        }

        Clients = clients;
    }

    public IEnumerable<EnvironmentInstance> Clients { get; }
    public EnvironmentInstance Server { get; }

    private TestNetworkRouter networkOrchestrator = new TestNetworkRouter();

    private EnvironmentInstance CreateClient()
    {
        var containerProvider = new ContainerProvider();

        var builder = new ContainerBuilder();

        builder.RegisterModule<ClientModule>();
        builder.RegisterType<MockClient>().AsSelf().As<INetwork>().As<ICoopClient>().InstancePerLifetimeScope();
        builder.RegisterType<ClientInstance>().AsSelf();
        builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();

        AddSharedDependencies(builder);

        var container = builder.Build();

        containerProvider.SetProvider(container);

        var instance = container.Resolve<ClientInstance>()!;

        networkOrchestrator.AddClient(instance);

        return instance;
    }

    private EnvironmentInstance CreateServer()
    {
        var containerProvider = new ContainerProvider();

        var builder = new ContainerBuilder();

        builder.RegisterModule<ServerModule>();
        builder.RegisterType<MockServer>().AsSelf().As<INetwork>().As<ICoopServer>().InstancePerLifetimeScope();
        builder.RegisterType<ServerInstance>().AsSelf();
        builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();

        AddSharedDependencies(builder);

        var container = builder.Build();

        containerProvider.SetProvider(container);

        var instance = container.Resolve<ServerInstance>()!;

        networkOrchestrator.AddServer(instance);

        return instance;
    }

    private ContainerBuilder AddSharedDependencies(ContainerBuilder builder)
    {
        builder.RegisterInstance(networkOrchestrator).AsSelf().SingleInstance();

        builder.RegisterType<TestMessageBroker>().As<IMessageBroker>().SingleInstance();
        builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
        builder.RegisterType<MockObjectManager>().As<IObjectManager>().InstancePerLifetimeScope();
        builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();
        builder.RegisterType<ControllerIdProvider>().As<IControllerIdProvider>().InstancePerLifetimeScope();
        builder.RegisterType<MockControlledEntityRegistry>().As<IControlledEntityRegistry>().InstancePerLifetimeScope();

        return builder;
    }
}

