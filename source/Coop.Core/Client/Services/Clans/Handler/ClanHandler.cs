using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using GameInterface.Services.Clans.Messages;
using Coop.Core.Client.Services.Clans.Messages;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Client.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans.
    /// </summary>
    public class ClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClanHandler>();

        public ClanHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ClanNameChange>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChange>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new ClanNameChangeRequest(payload.Clan.StringId, payload.Name, payload.InformalName));
        }
    }
}
