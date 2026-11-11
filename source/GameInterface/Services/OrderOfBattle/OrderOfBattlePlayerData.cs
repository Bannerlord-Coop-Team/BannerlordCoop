using GameInterface.Services.OrderOfBattle.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.OrderOfBattle;

/// <summary>
/// OrderOfBattleCampaignBehavior and NavalOrderOfBattleCampaignBehavior hold the order of battle for only one player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class OrderOfBattlePlayerData
{
    // Dictionary<PlayerHeroId, OrderOfBattleData>
    [ProtoMember(1)]
    public Dictionary<string, OrderOfBattleData> PlayerOrderOfBattles { get; }

    public OrderOfBattlePlayerData(
        Dictionary<string, OrderOfBattleData> playerOrderOfBattles)
    {
        PlayerOrderOfBattles = playerOrderOfBattles ?? new();
    }
}
