using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Caravans.Messages;

public record InitializeClientCaravansData : IEvent
{
    public CaravansPlayerData CaravansPlayerData;

    public InitializeClientCaravansData(CaravansPlayerData caravansPlayerData)
    {
        CaravansPlayerData = caravansPlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerCaravansDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerCaravansDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}