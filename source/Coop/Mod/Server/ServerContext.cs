using Common.MessageBroker;
using Coop.Mod.States;
using Coop.Mod.States.Server;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod
{
    public class ServerContext : IServerContext
    {
        public ILogger Logger => _logger;
        private readonly ILogger _logger;
        public IMessageBroker MessageBroker => _messageBroker;
        private readonly IMessageBroker _messageBroker;

        public IState State { get => _state; set => _state = (IServerState)value; }
        private IServerState _state;

        public IPacketManager PacketManager => _packetManager;
        private readonly IPacketManager _packetManager;

        public ServerContext(ILogger logger, IMessageBroker messageBroker, IPacketManager packetManager)
        {
            _logger = logger;
            _messageBroker = messageBroker;
            _packetManager = packetManager;

            _state = new InitialServerState(this);
        }
    }
}
