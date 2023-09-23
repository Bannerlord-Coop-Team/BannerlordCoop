using Common.Messaging;

namespace Coop.Core.Client.Services.Player.Messages;

public record NetworkNewPlayerData : IEvent
{
    public readonly byte[] HeroData;

    public NetworkNewPlayerData(byte[] heroData)
    {
        HeroData = heroData;
    }
}
