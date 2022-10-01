namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerTransitionCampaign
    {
        public PlayerTransitionCampaign(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
