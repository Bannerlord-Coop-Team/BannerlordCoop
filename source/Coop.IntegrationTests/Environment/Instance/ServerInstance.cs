using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ServerInstance : EnvironmentInstance
{
    protected override TestMessageBroker MessageBroker { get; }

    protected override MockNetworkBase MockNetwork { get; }

    public ServerInstance(TestMessageBroker messageBroker, MockServer server)
    {
        MessageBroker = messageBroker;
        MockNetwork = server;
    }
}
