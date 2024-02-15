using Common.Tests.Utils;
using Coop.Core;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ClientInstance : EnvironmentInstance
{
    public ClientInstance(TestMessageBroker messageBroker, MockClient client, IContainerProvider containerProvider) :
        base(messageBroker, client, containerProvider)
    {
    }
}
