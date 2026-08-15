using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages;

public record InitializeClientAgingData : IEvent
{
    public AgingPlayerData AgingPlayerData;

    public InitializeClientAgingData(AgingPlayerData agingPlayerData)
    {
        AgingPlayerData = agingPlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerAgingDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerAgingDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}