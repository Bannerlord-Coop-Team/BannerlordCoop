using Common.Messaging;
using GameInterface.Services.Inventory.TradeSkills.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Inventory.TradeSkills.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateTradeRumors : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly List<TradeRumorData> TradeRumors;

    [ProtoMember(3)]
    public readonly Dictionary<string, long> EnteredSettlements;

    public NetworkUpdateTradeRumors(
        string playerHeroId,
        List<TradeRumorData> tradeRumors,
        Dictionary<string, long> enteredSettlements)
    {
        PlayerHeroId = playerHeroId;
        TradeRumors = tradeRumors;
        EnteredSettlements = enteredSettlements;
    }
}
