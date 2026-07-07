using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.GameState;

/// <summary>
/// Game-state checks usable before any session container exists.
/// </summary>
public static class GameStateQuery
{
    public static bool IsAtMainMenu => GameStateManager.Current?.ActiveState is InitialState;
}
