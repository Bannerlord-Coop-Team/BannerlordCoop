using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.GameState.Interfaces;

public interface IMapStateInterface : IGameAbstraction
{
    /// <summary>[Game thread] Whether the campaign map is the active game state.</summary>
    bool IsMapActive();

    /// <summary>[Game thread] Whether a screen or mission state sits over the campaign map.</summary>
    bool IsMapCovered();
}

/// <summary>Reads where the campaign map sits in the game state stack.</summary>
internal class MapStateInterface : IMapStateInterface
{
    // Reads the static vanilla OpenMapConversation reads, so both see the same active state
    public bool IsMapActive() => GameStateManager.Current?.ActiveState is MapState;

    public bool IsMapCovered() => !IsMapActive() && GameStateManager.Current?.LastOrDefault<MapState>() != null;
}
