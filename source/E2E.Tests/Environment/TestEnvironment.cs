using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core;
using Coop.Core.Client;
using Coop.Core.Server;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using GameInterface;
using GameInterface.Policies;
using GameInterface.Surrogates;
using Xunit.Abstractions;

namespace E2E.Tests.Environment;

/// <summary>
/// Environment used for integration testing
/// </summary>
public class TestEnvironment
{
    private readonly TestNetworkRouter networkOrchestrator;


    /// <summary>
    /// Constructor for TestEnvironment
    /// </summary>
    /// <param name="numClients">Number of clients to create, defaults to 2 clients</param>
    public TestEnvironment(ITestOutputHelper output, int numClients = 2)
    {
        this.registerGameInterface = registerGameInterface;

        // Setup test network
        networkOrchestrator = new TestNetworkRouter();

        Server = CreateServer(output);

        var serverNetwork = Server.Container.Resolve<MockServer>();

        var clients = new EnvironmentInstance[numClients];
        for (int i = 0; i < numClients; i++)
        {
            clients[i] = CreateClient(output);
            serverNetwork.AddPeer(clients[i].NetPeer);
        }

        Clients = clients;
    }

    public IEnumerable<EnvironmentInstance> Clients { get; }
    public EnvironmentInstance Server { get; }


    private readonly bool registerGameInterface;

    private EnvironmentInstance CreateClient(ITestOutputHelper output) => new ClientInstance(networkOrchestrator);

    private EnvironmentInstance CreateServer(ITestOutputHelper output) => new ServerInstance(networkOrchestrator);

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
        builder.RegisterType<SurrogateCollection>().As<ISurrogateCollection>().InstancePerLifetimeScope().AutoActivate();

        return builder;
    }
}

