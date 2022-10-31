namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerTransferCharacter
    {
        public PlayerTransferCharacter(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
