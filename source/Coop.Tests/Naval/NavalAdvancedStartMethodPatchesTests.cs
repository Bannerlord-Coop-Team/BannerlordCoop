#if COOP_NAVAL
using GameInterface.Services.AdvancedStart.Patches;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit;

namespace Coop.Tests.Naval;

// The patch binds even if War Sails stops reading the campaign seed the way the shared transpiler rewrites it,
// in which case every player would get the same castle, port town or ship again.
public class NavalAdvancedStartMethodPatchesTests
{
    [Theory]
    [InlineData(nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsFleetAdmiral))]
    [InlineData(nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsMerchantVenturer))]
    [InlineData(nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.GivePersonalShip))]
    public void Transpiler_ReplacesEveryCampaignSeedRead(string methodName)
    {
        var instructions = Transpile(methodName);

        var getSeed = AccessTools.PropertyGetter(typeof(CampaignOptions), nameof(CampaignOptions.Seed));
        Assert.DoesNotContain(instructions, instruction => instruction.Calls(getSeed));
        Assert.Contains(instructions, instruction => instruction.operand is MethodInfo { Name: "GetPlayerSeed" });
    }

    [Fact]
    public void Transpiler_RoutesTheMerchantVenturerWorkshopThroughThePlayerOwnedCheck()
    {
        var instructions = Transpile(nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsMerchantVenturer));

        var applyByFree = AccessTools.Method(typeof(ChangeOwnerOfWorkshopAction), nameof(ChangeOwnerOfWorkshopAction.ApplyByFree));
        Assert.DoesNotContain(instructions, instruction => instruction.Calls(applyByFree));
        Assert.Contains(instructions, instruction => instruction.operand is MethodInfo { Name: "GiveStartWorkshop" });
    }

    private static CodeInstruction[] Transpile(string methodName)
    {
        var method = AccessTools.Method(typeof(NavalAdvancedStartingPlayerOptionsCampaignBehavior), methodName);
        return AdvancedStartMethodPatches.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToArray();
    }
}
#endif
