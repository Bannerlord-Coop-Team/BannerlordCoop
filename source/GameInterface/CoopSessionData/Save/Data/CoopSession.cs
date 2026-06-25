using GameInterface.Services.Alleys;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using ProtoBuf;

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
    AlleyPlayerData AlleyPlayerData { get; }
}

/// <inheritdoc cref="ICoopSession"/>
[ProtoContract]
public class CoopSession : ICoopSession
{
    [ProtoMember(1)]
    public string UniqueGameId { get; }
    [ProtoMember(2)]
    public Player[] Players { get; }
    [ProtoMember(3)]
    public CraftingPlayerData CraftingPlayerData { get; }
    [ProtoMember(4)]
    public WorkshopPlayerData WorkshopPlayerData { get; }
    [ProtoMember(5)]
    public AlleyPlayerData AlleyPlayerData { get; }

    public CoopSession(string uniqueGameId, Player[] players, CraftingPlayerData craftingPlayerData, WorkshopPlayerData workshopPlayerData, AlleyPlayerData alleyPlayerData)
    {
        UniqueGameId = uniqueGameId;
        Players = players;
        CraftingPlayerData = craftingPlayerData;
        WorkshopPlayerData = workshopPlayerData;
        AlleyPlayerData = alleyPlayerData;
    }
}
