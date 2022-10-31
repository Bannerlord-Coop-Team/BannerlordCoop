namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerCreatingCharacter
    {
        public PlayerCreatingCharacter(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
