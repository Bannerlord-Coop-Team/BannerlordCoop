#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class RamTickPatchTests
{
    // S15 A1: vanilla's ram tick runs on every client, and only its kill goes through the owner-only decision.
    [Fact]
    public void Transpiler_RoutesTheTicksOneKillThroughDieUnlessPuppet()
    {
        var original = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(MissionShipRam), nameof(MissionShipRam.OnTick)));

        var instrumented = RamTickPatch.Transpiler(original).ToList();

        Assert.Single(instrumented, code => code.operand is MethodInfo called && called.DeclaringType == typeof(NavalRamKills)
            && called.Name == nameof(NavalRamKills.DieUnlessPuppet));
        Assert.DoesNotContain(instrumented, code => code.operand is MethodInfo called && called.DeclaringType == typeof(Agent)
            && called.Name == nameof(Agent.Die));
    }

    // Q8: a DLC update that moves the kill call stops co-op from starting instead of letting puppets die on every client.
    [Fact]
    public void Transpiler_WithoutTheKillCall_FailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => RamTickPatch.Transpiler(Array.Empty<CodeInstruction>()).ToList());
    }
}
#endif
