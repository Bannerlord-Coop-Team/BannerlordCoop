using Common.Messaging;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.GuantletMapEventVisuals.Messages;

internal readonly struct GauntletMapEventVisualInitialized : IEvent
{
    public readonly GauntletMapEventVisual Instance;
    public readonly CampaignVec2 Position;
    public readonly bool IsVisible;

    public GauntletMapEventVisualInitialized(GauntletMapEventVisual instance, CampaignVec2 position, bool isVisible)
    {
        Instance = instance;
        Position = position;
        IsVisible = isVisible;
    }
}
