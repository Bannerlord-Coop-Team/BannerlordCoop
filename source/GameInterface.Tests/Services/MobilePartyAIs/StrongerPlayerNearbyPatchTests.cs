using GameInterface.Services.MobilePartyAIs.Patches;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>
/// The v1.5 stronger-party check in <c>GetBestInitiativeBehavior</c> must count active player parties in place
/// of the main party, while the method's older main-party read stays as it is.
/// </summary>
public class StrongerPlayerNearbyPatchTests
{
    [Fact]
    public void InstalledModel_SwapsOnlyTheStrongerMainPartyCheck()
    {
        var method = AccessTools.Method(typeof(DefaultMobilePartyAIModel), nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior));
        var original = PatchProcessor.GetOriginalInstructions(method).ToArray();
        var isMainParty = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.IsMainParty));
        var strength = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.EstimatedStrength));
        var helper = AccessTools.Method(typeof(DefaultMobilePartyAIModelPatches), nameof(DefaultMobilePartyAIModelPatches.IsActivePlayerParty));
        // v1.5.4 reads IsMainParty twice: the new stronger-party check, then the older nearby-party skip.
        Assert.Equal(2, original.Count(instruction => instruction.Calls(isMainParty)));

        var patched = DefaultMobilePartyAIModelPatches.StrongerPlayerNearbyTranspiler(original).ToArray();

        Assert.Single(patched.Where(instruction => instruction.Calls(isMainParty)));
        var swapped = Array.FindIndex(patched, instruction => instruction.Calls(helper));
        Assert.True(swapped >= 0);
        Assert.Contains(patched.Skip(swapped).Take(6), instruction => instruction.Calls(strength));
    }
}
