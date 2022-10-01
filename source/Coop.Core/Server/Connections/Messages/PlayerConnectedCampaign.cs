using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerConnectedCampaign : ICommand
    {
        public PlayerConnectedCampaign(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
