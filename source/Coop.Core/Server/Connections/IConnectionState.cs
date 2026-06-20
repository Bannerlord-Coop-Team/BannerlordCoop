using Common.LogicStates;
using System;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Represents the state of a single connection
/// </summary>
public interface IConnectionState : IState, IDisposable
{
    /// <summary>
    /// Whether this connection is loading game state and time must stay paused for it.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Whether this connection has finished joining and is now in the game (campaign or mission).
    /// </summary>
    bool HasJoinedGame { get; }

    /// <summary>
    /// Player is in the process of creating a character
    /// </summary>
    void CreateCharacter();

    /// <summary>
    /// New character info is being transferred to server
    /// </summary>
    void TransferSave();

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
