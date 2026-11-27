using GameInterface.Services.MobilePartyAIs.Patches;
using HarmonyLib;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>Verifies the native stronger-party check is the only IsMainParty read replaced.</summary>
public class DefaultMobilePartyAIModelPatchesTests
{
    [Fact]
    public void TranspilerSwapsOnlyTheStrongerPartyRead()
    {
        var getter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.IsMainParty));
        var scorer = AccessTools.Method(typeof(DefaultMobilePartyAIModel), nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior));
        var original = PatchProcessor.GetOriginalInstructions(scorer).ToList();
        var mainPartyReads = original.Where(code => code.Calls(getter)).ToArray();
        Assert.Equal(2, mainPartyReads.Length);

        var strength = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.EstimatedStrength));
        var strengthReads = Enumerable.Range(0, original.Count - 4)
            .Where(index => original[index].Calls(getter) && original[index + 4].Calls(strength));
        int strongerCheckIndex = Assert.Single(strengthReads);

        var patched = DefaultMobilePartyAIModelPatches.ReplaceStrongerMainPartyCheck(original).ToList();
        var untouchedRead = Assert.Single(patched, code => code.Calls(getter));
        Assert.Same(mainPartyReads[1], untouchedRead);
        Assert.Equal(original.Count, patched.Count);
        for (int i = 0; i < original.Count; i++)
        {
            if (i != strongerCheckIndex)
                Assert.Same(original[i], patched[i]);
        }

        var strongerPartyCheck = patched[strongerCheckIndex];
        Assert.Equal(OpCodes.Call, strongerPartyCheck.opcode);
        var activePlayerCheck = AccessTools.Method(typeof(DefaultMobilePartyAIModelPatches), "IsActivePlayerParty");
        Assert.True(strongerPartyCheck.Calls(activePlayerCheck));
    }
}
