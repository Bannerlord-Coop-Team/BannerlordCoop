namespace Coop.Core.Client.Messages
{
    public readonly struct NetworkConnected
    {
        public NetworkConnected(bool clientPartyExists)
        {
            ClientPartyExists = clientPartyExists;
        }

        public bool ClientPartyExists { get; }
    }
}
