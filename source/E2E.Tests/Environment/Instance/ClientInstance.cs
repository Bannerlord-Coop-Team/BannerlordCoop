using Common.Tests.Utils;
using Coop.Core;
using E2E.Tests.Environment.Mock;

namespace E2E.Tests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ClientInstance : EnvironmentInstance
{
    public ClientInstance(TestMessageBroker messageBroker, MockClient client, IContainerProvider containerProvider) :
        base(messageBroker, client, containerProvider)
    {
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
