using GameInterface.Services.GuantletMapEventVisuals;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

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
        var gauntletType = Assembly.Load("SandBox.GauntletUI").GetType("SandBox.GauntletUI.Map.GauntletMapEventVisual", true);
        var visual = (IMapEventVisual)FormatterServices.GetUninitializedObject(gauntletType);

        Assert.False(MapEventVisualSyncFilter.IsHeadless(visual));
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
