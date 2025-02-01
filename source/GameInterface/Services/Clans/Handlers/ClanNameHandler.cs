using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class ClanNameHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClanNameHandler>();

        public ClanNameHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ClanNameChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeClanName>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeClanName>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChanged> obj)
        {
            var payload = obj.What;
            bool isServer = ModInformation.IsServer;
            network.SendAll(new NetworkChangeClanName(payload.ClanId, payload.Name, payload.InformalName));
        }

        private void Handle(MessagePayload<NetworkChangeClanName> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Clan>(payload.ClanId, out var clan) == false)
            {
                Logger.Error("Unable to find clan ({clanId})", payload.ClanId);
                return;
            }

            ClanNameChangePatch.RunOriginalChangeClanName(clan, new TextObject(payload.Name), new TextObject(payload.InformalName));
        }
    }
}
