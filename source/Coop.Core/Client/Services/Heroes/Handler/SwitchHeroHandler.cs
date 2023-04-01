using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Client.Services.Heroes.Handler
{
    internal class SwitchHeroHandler
    {
        private readonly INetworkMessageBroker networkMessageBroker;
        private readonly IClientLogic clientLogic;

        public SwitchHeroHandler(
            INetworkMessageBroker networkMessageBroker,
            IClientLogic clientLogic)
        {
            this.networkMessageBroker = networkMessageBroker;
            this.clientLogic = clientLogic;
            networkMessageBroker.Subscribe<ExistingObjectGuidsLoaded>(Handle_ExistingObjectGuidsLoaded);
        }

        private void Handle_ExistingObjectGuidsLoaded(MessagePayload<ExistingObjectGuidsLoaded> obj)
        {
            var controlledHeroId = clientLogic.ControlledHeroId;
            if (controlledHeroId == default) return;

            networkMessageBroker.Publish(this, new SwitchToHero(controlledHeroId));
        }
    }
}
