using Common.Messaging;
using Coop.Core.Server.Services.Stances.Messages;
using GameInterface.Services.Stances.Messages;

namespace Coop.Core.Client.Services.Stances.Handlers
{
    /// <summary>
    /// Client side handler that republishes network faction stance changes as internal commands.
    /// </summary>
    public class ClientStanceHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public ClientStanceHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<NetworkDeclareWar>(HandleNetworkDeclareWar);
            messageBroker.Subscribe<NetworkMakePeace>(HandleNetworkMakePeace);
        }

        private void HandleNetworkDeclareWar(MessagePayload<NetworkDeclareWar> obj)
        {
            var payload = obj.What;
            messageBroker.Publish(this, new DeclareWarChanged(payload.Faction1Id, payload.Faction2Id, payload.Detail));
        }

        private void HandleNetworkMakePeace(MessagePayload<NetworkMakePeace> obj)
        {
            var payload = obj.What;
            messageBroker.Publish(this, new MakePeaceChanged(payload.Faction1Id, payload.Faction2Id, payload.DailyTribute, payload.DailyTributeDuration, payload.Detail));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkDeclareWar>(HandleNetworkDeclareWar);
            messageBroker.Unsubscribe<NetworkMakePeace>(HandleNetworkMakePeace);
        }
    }
}
