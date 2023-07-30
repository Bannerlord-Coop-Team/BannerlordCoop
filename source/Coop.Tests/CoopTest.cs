using Autofac;
using Coop.Tests.Mocks;
using GameInterface.Services.Entity;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Server;

namespace Coop.Tests
{
    public class CoopTest
    {
        public MockMessageBroker MockMessageBroker { get; }
        public MockNetwork MockNetwork { get; }
        public ITestOutputHelper Output { get; }
        public ServiceProvider ServiceProvider { get; }

        public CoopTest(ITestOutputHelper output)
        {
            Output = output;

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddScoped<MockMessageBroker>();
            serviceCollection.AddScoped<IMessageBroker, MockMessageBroker>(x => x.GetService<MockMessageBroker>()!);
            serviceCollection.AddScoped<MockNetwork>();
            serviceCollection.AddScoped<INetwork, MockNetwork>(x => x.GetService<MockNetwork>()!);
            serviceCollection.AddScoped<IControllerIdProvider, ControllerIdProvider>();

            serviceCollection.AddScoped<IClientLogic, ClientLogic>();
            serviceCollection.AddScoped<IServerLogic, ServerLogic>();

            ServiceProvider = serviceCollection.BuildServiceProvider();

            MockMessageBroker = ServiceProvider.GetService<MockMessageBroker>()!;
            MockNetwork = ServiceProvider.GetService<MockNetwork>()!;
        }
    }
}
