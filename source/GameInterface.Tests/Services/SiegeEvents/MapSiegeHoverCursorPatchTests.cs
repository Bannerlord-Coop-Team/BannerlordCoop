using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using SandBox.View.Map;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

// The hover cursor override must stay wired to the render-thread hover flow; the joined
// gate itself is shared with the popup and covered by SiegeDefenderCommandAuthorityTests.
public class MapSiegeHoverCursorPatchTests
{
    [Fact]
    public void HoverCursorPostfix_TargetsMapScreenHandleMouse()
    {
        var method = AccessTools.Method(typeof(MapScreen), "HandleMouse");

        Assert.NotNull(method);

        var postfix = AccessTools.Method(
            typeof(MapSiegeHoverCursorPatch),
            nameof(MapSiegeHoverCursorPatch.Postfix));

        Assert.NotNull(postfix);
    }
}
