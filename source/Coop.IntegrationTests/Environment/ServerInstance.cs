using Common.Messaging;
using Coop.Core.Server;
using Coop.IntegrationTests.Environment.Mock;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment
{
    public class ServerInstance : InstanceEnvironment
    {
        public override NetPeer NetPeer => _server.NetPeer;

        private MockServer _server;

        public ServerInstance(IMessageBroker messageBroker, MockServer server) : base(messageBroker)
        {
            _server = server;
        }
    }
}
