using Autofac;
using Common.Tests.Utils;
using E2E.Tests.Environment.Mock;

namespace E2E.Tests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ClientInstance : EnvironmentInstance
{
    public ClientInstance(TestMessageBroker messageBroker, MockClient client, ILifetimeScope container) :
        base(messageBroker, client, container)
    {
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
