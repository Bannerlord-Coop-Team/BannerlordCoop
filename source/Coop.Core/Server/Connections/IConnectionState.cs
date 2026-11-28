using GameInterface.Services.Players.Data;
using System;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Represents the state of a single connection
/// </summary>
public interface IConnectionState : IDisposable
{
    /// <summary>
    /// Whether this connection is loading game state.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Player is in the process of creating a character
    /// </summary>
    void CreateCharacter();

    /// <summary>
    /// Sends the join save for <paramref name="acknowledgedPlayer"/>, the registration the peer was just told it plays
    /// </summary>
    void TransferSave(Player acknowledgedPlayer);

    /// <summary>
    /// Player loading server data as a whole
    /// </summary>
    void Load();

    /// <summary>
    /// Player entering into campaign map
    /// </summary>
    void EnterCampaign();

    /// <summary>
    /// Player entering mission state
    /// </summary>
    void EnterMission();
}
