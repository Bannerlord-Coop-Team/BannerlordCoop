using Autofac;
using Common.Tests.Utils;
using Coop.Core;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ClientInstance : EnvironmentInstance
{
    protected override TestMessageBroker MessageBroker { get; }

    protected override MockNetworkBase MockNetwork { get; }

    public ClientInstance(TestMessageBroker messageBroker, MockClient client)
    {
        MessageBroker = messageBroker;
        MockNetwork = client;
    }
}
