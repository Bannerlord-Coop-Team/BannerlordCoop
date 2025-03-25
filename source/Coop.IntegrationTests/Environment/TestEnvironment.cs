using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core;
using Coop.Core.Client;
using Coop.Core.Server;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment.Mock;
using GameInterface;
using GameInterface.Policies;
using GameInterface.Services.Entity;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Environment used for integration testing
/// </summary>
public class TestEnvironment
{
    private Core.ContainerProvider containerProvider;
    public IContainer Container => containerProvider.GetContainer();

    private readonly TestNetworkRouter networkOrchestrator;


    /// <summary>
    /// Constructor for TestEnvironment
    /// </summary>
    /// <param name="numClients">Number of clients to create, defaults to 2 clients</param>
    public TestEnvironment(int numClients = 2, bool registerGameInterface = false)
    {
        this.registerGameInterface = registerGameInterface;

        // Setup test network
        networkOrchestrator = new TestNetworkRouter();

        Server = CreateServer();

        var serverNetwork = Server.Container.Resolve<MockServer>();

        var clients = new EnvironmentInstance[numClients];
        for (int i = 0; i < numClients; i++)
        {
            clients[i] = CreateClient();
            serverNetwork.AddPeer(clients[i].NetPeer);
        }

        Clients = clients;
    }

    public IEnumerable<EnvironmentInstance> Clients { get; }
    public EnvironmentInstance Server { get; }

    
    private readonly bool registerGameInterface;

    private EnvironmentInstance CreateClient()
    {
        containerProvider = new Core.ContainerProvider();

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
        containerProvider = new Core.ContainerProvider();

        var builder = new ContainerBuilder();

        builder.RegisterModule<ServerModule>();
        builder.RegisterType<MockServer>().AsSelf().As<INetwork>().As<ICoopServer>().InstancePerLifetimeScope();
        builder.RegisterType<ControlledEntityRegistry>().As<IControlledEntityRegistry>().InstancePerLifetimeScope();
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
        if (registerGameInterface)
        {
            builder.RegisterModule<GameInterfaceModule>();
        }

        builder.RegisterInstance(networkOrchestrator).AsSelf().SingleInstance();

        builder.RegisterType<TestMessageBroker>().AsSelf().As<IMessageBroker>().InstancePerLifetimeScope();
        builder.RegisterType<TestPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();
        builder.RegisterType<SerializableTypeMapper>().As<ISerializableTypeMapper>().SingleInstance();

        return builder;
    }
}

