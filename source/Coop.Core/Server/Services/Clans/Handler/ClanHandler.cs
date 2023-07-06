using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using Coop.Core.Client.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;

namespace Coop.Core.Server.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans.
    /// </summary>
    public class ClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClanHandler>();

        public ClanHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ClanNameChangeRequest>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChangeRequest>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChangeRequest> obj)
        {
            var payload = obj.What;

            ClanNameChangeApproved clanNameChangeApproved = new ClanNameChangeApproved(payload.ClanId, payload.Name, payload.InformalName);

            messageBroker.Publish(this, clanNameChangeApproved);

            network.SendAll(clanNameChangeApproved);
        }
    }
}
