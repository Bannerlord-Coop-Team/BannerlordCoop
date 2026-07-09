using GameInterface.Services.Alleys;
using GameInterface.Services.Caravans;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using ProtoBuf;
using System;

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
}

/// <inheritdoc cref="ICoopSession"/>
[ProtoContract]
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
        new InteractionsPlayerData(new(), new(), new(), new()));

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

    public CoopSession(
        string uniqueGameId,
        Player[] players,
        CraftingPlayerData craftingPlayerData,
        WorkshopPlayerData workshopPlayerData,
        CaravansPlayerData caravansPlayerData,
        AlleyPlayerData alleyPlayerData,
        InteractionsPlayerData interactionsPlayerData)
    {
        UniqueGameId = uniqueGameId;
        Players = players;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        CaravansPlayerData = caravansPlayerData;
        AlleyPlayerData = alleyPlayerData;
        InteractionsPlayerData = interactionsPlayerData;
    }
}
