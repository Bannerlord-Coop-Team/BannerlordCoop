using Common.Messaging;

namespace Coop.Core.Client.Services.Player.Messages;

// TODO remove if not used
public record NetworkNewPlayerData : IEvent
{
    public readonly byte[] HeroData;
    public readonly string PlayerId;

    public NetworkNewPlayerData(string playerId, byte[] heroData)
    {
        PlayerId = playerId;
        HeroData = heroData;
    }
}
