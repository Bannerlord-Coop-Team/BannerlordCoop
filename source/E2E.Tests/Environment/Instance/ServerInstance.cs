using Autofac;
using Common.Tests.Utils;
using E2E.Tests.Environment.Mock;

namespace E2E.Tests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
public class ServerInstance : EnvironmentInstance
{
    public ServerInstance(TestMessageBroker messageBroker, MockServer server, ILifetimeScope container) :
        base(messageBroker, server, container)
    {
    }

    public override void Dispose()
    {
        Container.Dispose();
    }
}
