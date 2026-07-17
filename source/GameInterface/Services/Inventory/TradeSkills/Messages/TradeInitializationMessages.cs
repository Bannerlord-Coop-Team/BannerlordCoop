using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Inventory.TradeSkills.Messages;

public record InitializeClientTradeData : IEvent
{
    public TradePlayerData TradePlayerData;

    public InitializeClientTradeData(TradePlayerData tradePlayerData)
    {
        TradePlayerData = tradePlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerTradeDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerTradeDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}