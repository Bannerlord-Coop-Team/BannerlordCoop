using Common.Messaging;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
internal class ServerInstance : EnvironmentInstance
{
    public ServerInstance(IMessageBroker messageBroker, MockServer server) :
        base(messageBroker, server)
    {
    }
}
