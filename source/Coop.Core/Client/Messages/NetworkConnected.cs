using Common.Messaging;

namespace Coop.Core.Client.Messages
{
    public readonly struct NetworkConnected : IEvent
    {
        public bool ClientPartyExists { get; }

        public NetworkConnected(bool clientPartyExists)
        {
            ClientPartyExists = clientPartyExists;
        }
    }
}
