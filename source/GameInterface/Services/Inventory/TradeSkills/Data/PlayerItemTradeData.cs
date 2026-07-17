using ProtoBuf;

namespace GameInterface.Services.Inventory.TradeSkills.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct PlayerItemTradeData
{
    [ProtoMember(1)]
    public readonly float AveragePrice;

    [ProtoMember(2)]
    public readonly int NumItemsPurchased;

    public PlayerItemTradeData(float averagePrice, int numItemsPurchased)
    {
        AveragePrice = averagePrice;
        NumItemsPurchased = numItemsPurchased;
    }
}
