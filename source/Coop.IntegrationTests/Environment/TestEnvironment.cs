using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common;
using Coop.Core.Server;
using Coop.IntegrationTests.Environment.Mock;
using Microsoft.Extensions.DependencyInjection;
using Coop.Core.Server.Services.Save;

namespace Coop.IntegrationTests.Environment;

public class TestEnvironment
{
    public TestEnvironment(int numClients = 2)
    {
        Server = CreateServer();

        List<InstanceEnvironment> clients = new List<InstanceEnvironment>();
        for (int i = 0; i < numClients; i++)
        {
            clients.Add(CreateClient());
        }

        Clients = clients;
    }

    public IEnumerable<InstanceEnvironment> Clients { get; }
    public InstanceEnvironment Server { get; }

    private List<IHandler> _handlers = new List<IHandler>();

    private TestNetworkOrchestrator networkOrchestrator = new TestNetworkOrchestrator();

    public InstanceEnvironment CreateClient()
    {
        var handlerTypes = HandlerCollector.Collect<ClientModule>();
        var serviceCollection = new ServiceCollection();

        foreach (var handlerType in handlerTypes)
        {
            serviceCollection.AddScoped(handlerType);
        }

        serviceCollection.AddScoped<MockClient>();
        serviceCollection.AddScoped<INetwork, MockClient>(x => x.GetService<MockClient>()!);
        serviceCollection.AddScoped<ICoopClient, MockClient>(x => x.GetService<MockClient>()!);
        serviceCollection.AddScoped<IMessageBroker, TestMessageBroker>();
        
        serviceCollection.AddScoped<ClientInstance>();
        serviceCollection.AddSingleton(networkOrchestrator);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        foreach (var handlerType in handlerTypes)
        {
            _handlers.Add((IHandler)serviceProvider.GetService(handlerType)!);
        }

        var instance = serviceProvider.GetService<ClientInstance>()!;

        networkOrchestrator.AddClient(instance);

        return instance;
    }

    public InstanceEnvironment CreateServer()
    {
        var handlerTypes = HandlerCollector.Collect<ServerModule>();
        var serviceCollection = new ServiceCollection();

        foreach (var handlerType in handlerTypes)
        {
            serviceCollection.AddScoped(handlerType);
        }

        serviceCollection.AddScoped<MockServer>();
        serviceCollection.AddScoped<INetwork, MockServer>(x => x.GetService<MockServer>()!);
        serviceCollection.AddScoped<ICoopServer, MockServer>(x => x.GetService<MockServer>()!);
        serviceCollection.AddScoped<IMessageBroker, TestMessageBroker>();
        serviceCollection.AddScoped<ICoopSaveManager, CoopSaveManager>();
        serviceCollection.AddScoped<ServerInstance>();
        serviceCollection.AddSingleton(networkOrchestrator);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        foreach (var handlerType in handlerTypes)
        {
            _handlers.Add((IHandler)serviceProvider.GetService(handlerType)!);
        }

        var instance = serviceProvider.GetService<ServerInstance>()!;

        networkOrchestrator.AddServer(instance);

        return instance;
    }
}

