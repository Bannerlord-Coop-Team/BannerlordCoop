using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Handlers
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
            messageBroker.Subscribe<ClanNameChangeApproved>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChangeApproved>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChangeApproved> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            ClanNameChangePatch.RunOriginalChangeClanName(clan, new TextObject(payload.Name), new TextObject(payload.InformalName));
        }
    }
}
