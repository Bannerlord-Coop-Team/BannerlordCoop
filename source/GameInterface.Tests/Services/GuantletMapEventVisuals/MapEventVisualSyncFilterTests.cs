using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.GuantletMapEventVisuals;

public class MapEventVisualSyncFilterTests
{
    [Fact]
    public void ObfuscatedServerVisual_IsHeadless()
    {
        // The released dedicated server names its visual A.f+A, so a name check never matched it.
        Assert.True(MapEventVisualSyncFilter.IsHeadless(new f.A()));
    }

    [Fact]
    public void GauntletVisual_IsNotHeadless()
    {
        Assert.False(MapEventVisualSyncFilter.IsHeadless(ObjectHelper.SkipConstructor<GauntletMapEventVisual>()));
    }

    private sealed class f
    {
        public sealed class A : IMapEventVisual
        {
            public void Initialize(CampaignVec2 position, bool isVisible) { }
            public void OnMapEventEnd() { }
            public void SetVisibility(bool isVisible) { }
        }
    }
}
