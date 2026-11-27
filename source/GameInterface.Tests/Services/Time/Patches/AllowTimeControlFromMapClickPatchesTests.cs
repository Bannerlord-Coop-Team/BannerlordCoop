using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using SandBox.View.Map;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Time.Patches;

/// <summary>
/// The HandleMouse transpiler must reroute the double click timeout's TimeControlMode write, which drops a single
/// click's fast forward back to play, through the map click option check.
/// </summary>
public class AllowTimeControlFromMapClickPatchesTests
{
    [Fact]
    public void HandleMouseTranspiler_ReroutesTheTimeControlModeWrite()
    {
        var setter = AccessTools.PropertySetter(typeof(Campaign), nameof(Campaign.TimeControlMode));
        var reroute = AccessTools.Method(typeof(AllowTimeControlFromMapClickPatches), nameof(AllowTimeControlFromMapClickPatches.SetTimeControlModeFromMapClick));
        var original = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(MapScreen), nameof(MapScreen.HandleMouse)));

        var patched = AllowTimeControlFromMapClickPatches.HandleMouseTranspiler(original.Select(code => code.Clone())).ToList();

        Assert.DoesNotContain(patched, code => code.Calls(setter));
        Assert.Single(patched, code => code.Calls(reroute));
    }
}
