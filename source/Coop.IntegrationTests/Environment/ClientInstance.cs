using Common.Messaging;
using Coop.IntegrationTests.Environment.Mock;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment;

public class ClientInstance : InstanceEnvironment
{
    public override NetPeer NetPeer => _client.NetPeer;

    private MockClient _client;

    public ClientInstance(IMessageBroker messageBroker, MockClient client) : base(messageBroker)
    {
        _client = client;
    }
}
