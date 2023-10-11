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
    public class ClanLeaderHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClanLeaderHandler>();

        public ClanLeaderHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ChangeClanLeader>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeClanLeader>(Handle);
        }

        private void Handle(MessagePayload<ChangeClanLeader> obj)
        {
            var payload = obj.What;

            objectManager.TryGetObject<Clan>(payload.ClanId, out var clan);

            if (payload.NewLeaderId != null)
            {
                objectManager.TryGetObject<Hero>(payload.NewLeaderId, out var newLeader);
                ClanLeaderChangePatch.RunOriginalChangeClanLeader(clan, newLeader);
                return;
            }

            ClanLeaderChangePatch.RunOriginalChangeClanLeader(clan);

        }
    }
}