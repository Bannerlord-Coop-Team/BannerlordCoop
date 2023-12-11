using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameDebug.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Player.Handlers
{
    public class PlayerConnectionClientMessageHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public PlayerConnectionClientMessageHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
        }

        internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
        {
            messageBroker.Publish(this, new SendInformationMessage("A new player is joining the game, pausing"));
        }
    }
}
