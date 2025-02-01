using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventParties.Handlers
{
    internal class MapEventPartyLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MapEventPartyLifetimeHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public MapEventPartyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<MapEventPartyCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateMapEventParty>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<MapEventPartyCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateMapEventParty>(Handle);
        }

        private void Handle(MessagePayload<MapEventPartyCreated> payload)
        {
            var obj = payload.What;

            if (objectManager.AddNewObject(obj.MapEventParty, out var newId) == false) return;

            if (objectManager.TryGetId(obj.PartyBase, out string partyBaseId) == false) return;

            network.SendAll(new NetworkCreateMapEventParty(newId, partyBaseId));
        }

        private void Handle(MessagePayload<NetworkCreateMapEventParty> payload)
        {
            var obj = payload.What;

            if (objectManager.TryGetObject(obj.PartyBaseId, out PartyBase partyBase) == false) return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    var newMapEventParty = ObjectHelper.SkipConstructor<MapEventParty>();

                    if (objectManager.AddExisting(obj.MapEventPartyId, newMapEventParty) == false)
                    {
                        Logger.Error("Failed to create party with id {stringId}", obj.MapEventPartyId);
                        return;
                    }
                }
            });
        }
    }
}
