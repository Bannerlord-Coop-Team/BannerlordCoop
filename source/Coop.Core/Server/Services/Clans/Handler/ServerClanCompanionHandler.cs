using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;
using Serilog;
using System;

namespace Coop.Core.Server.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans on server.
    /// </summary>
    public class ServerClanCompanionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanCompanionHandler>();

        public ServerClanCompanionHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<CompanionAdded>(Handle);
            messageBroker.Subscribe<NetworkAddCompanionRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CompanionAdded>(Handle);
            messageBroker.Unsubscribe<NetworkAddCompanionRequest>(Handle);
        }
        private void Handle(MessagePayload<CompanionAdded> obj)
        {
            var payload = obj.What;

            AddCompanion addCompanion = new AddCompanion(payload.ClanId, payload.CompanionId);

            messageBroker.Publish(this, addCompanion);

            NetworkCompanionAddApproved companionAddApproved = new NetworkCompanionAddApproved(payload.ClanId, payload.CompanionId);

            network.SendAll(companionAddApproved);
        }

        private void Handle(MessagePayload<NetworkAddCompanionRequest> obj)
        {
            var payload = obj.What;

            AddCompanion addCompanion = new AddCompanion(payload.ClanId, payload.CompanionId);

            messageBroker.Publish(this, addCompanion);

            NetworkCompanionAddApproved companionAddApproved = new NetworkCompanionAddApproved(payload.ClanId, payload.CompanionId);

            network.SendAll(companionAddApproved);
        }
    }
}