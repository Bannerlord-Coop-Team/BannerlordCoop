using Common.Messaging;
using Coop.IntegrationTests.Environment.Mock;

namespace Coop.IntegrationTests.Environment.Instance;

/// <inheritdoc cref="EnvironmentInstance"/>
internal class ClientInstance : EnvironmentInstance
{
    public ClientInstance(IMessageBroker messageBroker, MockClient client) :
        base(messageBroker, client)
    {
    }
}
