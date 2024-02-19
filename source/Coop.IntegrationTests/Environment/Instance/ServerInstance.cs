using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ServerInstance : EnvironmentInstance
{
    public ServerInstance(TestMessageBroker messageBroker, MockServer server, IContainerProvider containerProvider) :
        base(messageBroker, server, containerProvider)
    {
    }
}
