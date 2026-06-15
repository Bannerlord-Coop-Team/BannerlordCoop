using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Stances.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Stances.Messages;

namespace Coop.Core.Server.Services.Stances.Handlers
{
    /// <summary>
    /// Handles network related data for faction stance changes (war / peace).
    /// </summary>
    public class ServerStanceHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;

        public ServerStanceHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<FactionWarDeclared>(HandleLocalWarDeclared);
            messageBroker.Subscribe<FactionPeaceMade>(HandleLocalPeaceMade);
        }

        private void HandleLocalWarDeclared(MessagePayload<FactionWarDeclared> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Faction1, out var faction1Id)) return;
            if (!objectManager.TryGetIdWithLogging(payload.Faction2, out var faction2Id)) return;

            network.SendAll(new NetworkDeclareWar(faction1Id, faction2Id, payload.Detail));
        }

        private void HandleLocalPeaceMade(MessagePayload<FactionPeaceMade> obj)
        {
            var payload = obj.What;

            if (!objectManager.TryGetIdWithLogging(payload.Faction1, out var faction1Id)) return;
            if (!objectManager.TryGetIdWithLogging(payload.Faction2, out var faction2Id)) return;

            network.SendAll(new NetworkMakePeace(faction1Id, faction2Id, payload.DailyTribute, payload.DailyTributeDuration, payload.Detail));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<FactionWarDeclared>(HandleLocalWarDeclared);
            messageBroker.Unsubscribe<FactionPeaceMade>(HandleLocalPeaceMade);
        }
    }
}
