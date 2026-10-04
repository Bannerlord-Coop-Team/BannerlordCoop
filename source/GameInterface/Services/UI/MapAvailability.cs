using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

/// <summary>Tells the map overlays whether the campaign map is free for them to show.</summary>
public interface IMapAvailability
{
    /// <summary>True on the actual campaign map, not a map ticking behind a menu, mission, loading window, inquiry or conversation.</summary>
    bool IsMapAvailable();
}

/// <inheritdoc cref="IMapAvailability"/>
internal sealed class MapAvailability : IMapAvailability
{
    public bool IsMapAvailable() => ScreenManager.TopScreen is MapScreen &&
        GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !LoadingWindow.IsLoadingWindowActive && !InformationManager.IsAnyInquiryActive() &&
        Campaign.Current?.ConversationManager?.IsConversationInProgress != true;
}
