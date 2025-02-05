using Common.Tests.Utils;
using Coop.Core;
using E2E.Tests.Environment.Mock;

namespace E2E.Tests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ServerInstance : EnvironmentInstance
{
    public ServerInstance(TestMessageBroker messageBroker, MockServer server, IContainerProvider containerProvider) :
        base(messageBroker, server, containerProvider)
    {
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
