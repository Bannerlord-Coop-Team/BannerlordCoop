using GameInterface.Services.Alleys;
using GameInterface.Services.CampaignService.Statistics;
using GameInterface.Services.Caravans;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes;
using GameInterface.Services.Inventory;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.OrderOfBattle;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Traits;
using GameInterface.Services.Workshops;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace GameInterface.CoopSessionData.Save.Data;

/// <summary>
/// Represents the current state of the game that the game transfer
/// couldn't handle
/// </summary>
public interface ICoopSession
{
    string UniqueGameId { get; }
    Player[] Players { get; }
    CraftingPlayerData CraftingPlayerData { get; }
    WorkshopPlayerData WorkshopPlayerData { get; }
    CaravansPlayerData CaravansPlayerData { get; }
    AlleyPlayerData AlleyPlayerData { get; }
    InteractionsPlayerData InteractionsPlayerData { get; }
    TradePlayerData TradePlayerData { get; }
    InventoryPlayerData InventoryPlayerData { get; }
    HeroMeetingData HeroMeetingData { get; }
    AgingPlayerData AgingPlayerData { get; }
    Dictionary<string, ClanFinanceSettings> ClanFinance { get; }
    PlayerTraitData PlayerTraitData { get; }
    OrderOfBattlePlayerData OrderOfBattlePlayerData { get; }
    StatisticsPlayerData StatisticsPlayerData { get; }
}

[ProtoContract(SkipConstructor = true)]
public class CoopSession : ICoopSession
{
    // Shared "no data yet" shape for a fresh session (before any GameSaved/GameLoaded). A property,
    // not a static instance, otherwise every caller aliases the same mutable dictionaries and
    // AddPlayerKeys-style in-place edits leak into every other reader for the life of the process.
    public static CoopSession Empty => new CoopSession(
        string.Empty,
        Array.Empty<Player>(),
        new CraftingPlayerData(new(), new(), new()),
        new WorkshopPlayerData(new()),
        new CaravansPlayerData(new(), new()),
        new AlleyPlayerData(new()),
        new InteractionsPlayerData(new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()),
        new TradePlayerData(new(), new(), new(), new(), new()),
        new InventoryPlayerData(new(), new()),
        new HeroMeetingData(new()),
        new AgingPlayerData(new()));

    [ProtoMember(1)]
    public string UniqueGameId { get; }
    [ProtoMember(2)]
    public Player[] Players { get; }
    [ProtoMember(3)]
    public CraftingPlayerData CraftingPlayerData { get; }
    [ProtoMember(4)]
    public WorkshopPlayerData WorkshopPlayerData { get; }
    [ProtoMember(5)]
    public CaravansPlayerData CaravansPlayerData { get; }
    [ProtoMember(6)]
    public AlleyPlayerData AlleyPlayerData { get; }
    [ProtoMember(7)]
    public InteractionsPlayerData InteractionsPlayerData { get; }
    [ProtoMember(8)]
    public TradePlayerData TradePlayerData { get; }
    [ProtoMember(9)]
    public InventoryPlayerData InventoryPlayerData { get; }
    [ProtoMember(10)]
    public HeroMeetingData HeroMeetingData { get; }
    [ProtoMember(11)]
    public AgingPlayerData AgingPlayerData { get; }
    [ProtoMember(12)]
    public Dictionary<string, ClanFinanceSettings> ClanFinance { get; }
    [ProtoMember(13)]
    public PlayerTraitData PlayerTraitData { get; }
    [ProtoMember(14)]
    public OrderOfBattlePlayerData OrderOfBattlePlayerData { get; }
    [ProtoMember(15)]
    public StatisticsPlayerData StatisticsPlayerData { get; }

    public CoopSession(
        string uniqueGameId,
        Player[] players,
        CraftingPlayerData craftingPlayerData,
        WorkshopPlayerData workshopPlayerData,
        CaravansPlayerData caravansPlayerData,
        AlleyPlayerData alleyPlayerData,
        InteractionsPlayerData interactionsPlayerData,
        TradePlayerData tradePlayerData,
        InventoryPlayerData inventoryPlayerData,
        HeroMeetingData heroMeetingData,
        AgingPlayerData agingPlayerData,
        Dictionary<string, ClanFinanceSettings> clanFinance = null,
        PlayerTraitData playerTraitData = null,
        OrderOfBattlePlayerData orderOfBattlePlayerData = null,
        StatisticsPlayerData statisticsPlayerData = null)
    {
        UniqueGameId = uniqueGameId;
        Players = players;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        CaravansPlayerData = caravansPlayerData;
        AlleyPlayerData = alleyPlayerData;
        InteractionsPlayerData = interactionsPlayerData;
        TradePlayerData = tradePlayerData;
        InventoryPlayerData = inventoryPlayerData;
        HeroMeetingData = heroMeetingData;
        AgingPlayerData = agingPlayerData;
        ClanFinance = clanFinance ?? new();
        PlayerTraitData = playerTraitData ?? new PlayerTraitData(new());
        OrderOfBattlePlayerData = orderOfBattlePlayerData ?? new OrderOfBattlePlayerData(new());
        StatisticsPlayerData = statisticsPlayerData ?? new StatisticsPlayerData(new());
    }
}
