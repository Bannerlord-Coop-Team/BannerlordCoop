using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace GameInterface.Services.Bandits.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class BanditSpawningHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<BanditSpawningHandler>();

        public BanditSpawningHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<SpawnBandits>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SpawnBandits>(Handle);
        }

        private void Handle(MessagePayload<SpawnBandits> obj)
        {
            var payload = obj.What;

            objectManager.TryGetObject<Clan>(payload.ClanId, out var clan);

            BanditsSpawnPatch.RunOriginalSpawnAPartyInFaction(clan);
        }
    }
}
