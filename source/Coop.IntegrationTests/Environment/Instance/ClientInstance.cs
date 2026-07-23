using Autofac;
using Common.Tests.Utils;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ClientInstance : EnvironmentInstance
{
    public ClientInstance(TestMessageBroker messageBroker, MockClient client, ILifetimeScope container) :
        base(messageBroker, client, container)
    {
    }
}
