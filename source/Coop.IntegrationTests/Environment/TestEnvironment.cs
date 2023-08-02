using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common;
using Coop.Core.Server;
using Coop.IntegrationTests.Environment.Mock;
using Microsoft.Extensions.DependencyInjection;
using Coop.Core.Server.Services.Save;
using Coop.IntegrationTests.Environment.Instance;
using Common.PacketHandlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Entity;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Environment for integration testing
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

        List<EnvironmentInstance> clients = new List<EnvironmentInstance>();
        for (int i = 0; i < numClients; i++)
        {
            clients.Add(CreateClient());
        }

        Clients = clients;
    }

    public IEnumerable<EnvironmentInstance> Clients { get; }
    public EnvironmentInstance Server { get; }

    private List<object> _handlers = new List<object>();

    private TestNetworkRouter networkOrchestrator = new TestNetworkRouter();

    private EnvironmentInstance CreateClient()
    {
        var handlerTypes = HandlerCollector.Collect<ClientModule>();
        var serviceCollection = new ServiceCollection();

        AddSharedDependencies(serviceCollection);

        foreach (var handlerType in handlerTypes)
        {
            serviceCollection.AddScoped(handlerType);
        }

        

        serviceCollection.AddScoped<MockClient>();
        serviceCollection.AddScoped<INetwork, MockClient>(x => x.GetService<MockClient>()!);
        serviceCollection.AddScoped<ICoopClient, MockClient>(x => x.GetService<MockClient>()!);
        

        serviceCollection.AddScoped<ClientInstance>();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        foreach (var handlerType in handlerTypes)
        {
            _handlers.Add(serviceProvider.GetService(handlerType)!);
        }

        var instance = serviceProvider.GetService<ClientInstance>()!;

        networkOrchestrator.AddClient(instance);

        return instance;
    }

    private EnvironmentInstance CreateServer()
    {
        var handlerTypes = HandlerCollector.Collect<ServerModule>();
        var serviceCollection = new ServiceCollection();

        AddSharedDependencies(serviceCollection);

        foreach (var handlerType in handlerTypes)
        {
            serviceCollection.AddScoped(handlerType);
        }

        serviceCollection.AddScoped<MockServer>();
        serviceCollection.AddScoped<INetwork, MockServer>(x => x.GetService<MockServer>()!);
        serviceCollection.AddScoped<ICoopServer, MockServer>(x => x.GetService<MockServer>()!);
        serviceCollection.AddScoped<ServerInstance>();
        

        var serviceProvider = serviceCollection.BuildServiceProvider();

        foreach (var handlerType in handlerTypes)
        {
            _handlers.Add(serviceProvider.GetService(handlerType)!);
        }

        var instance = serviceProvider.GetService<ServerInstance>()!;

        networkOrchestrator.AddServer(instance);

        return instance;
    }

    private IServiceCollection AddSharedDependencies(IServiceCollection services)
    {
        services.AddSingleton(networkOrchestrator);

        services.AddScoped<IMessageBroker, TestMessageBroker>();
        services.AddScoped<IPacketManager, PacketManager>();
        services.AddScoped<IObjectManager, MockObjectManager>();
        services.AddScoped<ICoopSaveManager, CoopSaveManager>();
        services.AddScoped<IControllerIdProvider, ControllerIdProvider>();
        services.AddScoped<IControlledEntityRegistry, MockControlledEntityRegistry>();

        return services;
    }
}

