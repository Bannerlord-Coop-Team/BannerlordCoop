using Common.MessageBroker;
using Coop.Mod.States;
using Coop.Mod.States.Client;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Client
{
    public class ClientContext : IClientContext
    {
        public ILogger Logger => _logger;
        private readonly ILogger _logger;


        public IMessageBroker MessageBroker => _messageBroker;
        private readonly IMessageBroker _messageBroker;

        public IState State { get => _state; set => _state = (IClientState)value; }
        private IClientState _state;

        public IPacketManager PacketManager => _packetManager;
        private IPacketManager _packetManager;

        public ClientContext(ILogger logger, IMessageBroker messageBroker, IPacketManager packetManager)
        {
            _logger = logger;
            _messageBroker = messageBroker;
            _packetManager = packetManager;

            _state = new InitialClientState(this);
        }
    }
}
