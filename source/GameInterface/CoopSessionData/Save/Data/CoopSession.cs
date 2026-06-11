using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
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

    public CoopSession(string uniqueGameId, Player[] players, CraftingPlayerData craftingPlayerData)
    {
        UniqueGameId = uniqueGameId;
        Players = players;
        CraftingPlayerData = craftingPlayerData;
    }
}
