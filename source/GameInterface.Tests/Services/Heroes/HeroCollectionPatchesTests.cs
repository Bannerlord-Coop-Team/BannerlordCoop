using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

/// <summary>
/// Hero collection writes that happen only on the server must go through the sync intercepts.
/// </summary>
public class HeroCollectionPatchesTests
{
    [Fact]
    public void GarrisonAutoRecruitment_PublishesTheVolunteerItTakes()
    {
        var method = AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), nameof(GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange));
        var targets = (IEnumerable<MethodBase>)AccessTools.Method(typeof(HeroCollectionPatches), "TargetMethods").Invoke(null, null);
        Assert.Contains(method, targets);

        var transpiler = AccessTools.Method(typeof(HeroCollectionPatches), "VolunteerTranspiler");
        var original = PatchProcessor.GetOriginalInstructions(method).ToList();
        var patched = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { original })).ToList();

        // Garrison recruitment empties the notable's volunteer slot; that store must go through the sync intercept.
        Assert.Single(original.Where(instruction => instruction.opcode == OpCodes.Stelem_Ref));
        Assert.DoesNotContain(patched, instruction => instruction.opcode == OpCodes.Stelem_Ref);
        Assert.Contains(patched, instruction => instruction.Calls(
            AccessTools.Method(typeof(HeroCollectionPatches), nameof(HeroCollectionPatches.ArrayAssignIntercept))));
    }
}
