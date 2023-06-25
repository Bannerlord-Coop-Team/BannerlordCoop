using Common.LogicStates;
using System;

namespace Coop.Core.Client.States;

/// <summary>
/// Client State Machine Actions
/// </summary>
public interface IClientState : IState, IDisposable
{
    /// <summary>
    /// Connect to Coop Server
    /// </summary>
    void Connect();

    /// <summary>
    /// Disconnect from Coop Server
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Begin character creation for Coop Server
    /// </summary>
    void StartCharacterCreation();

    /// <summary>
    /// Load Data before entering Coop Server
    /// </summary>
    void LoadSavedData();

    /// <summary>
    /// Exit Bannerlord
    /// </summary>
    void ExitGame();

    /// <summary>
    /// Enter Bannerlord's main menu
    /// </summary>
    void EnterMainMenu(); 

    /// <summary>
    /// Join coop server campaign map
    /// </summary>
    void EnterCampaignState();

    /// <summary>
    /// Join p2p mission (battle) instance
    /// </summary>
    void EnterMissionState();

    /// <summary>
    /// Validates game data before starting join process
    /// </summary>
    void ValidateModules();
}
