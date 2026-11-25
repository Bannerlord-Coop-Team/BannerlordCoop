using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>Verifies the native stronger-party check is the only IsMainParty read replaced.</summary>
public class DefaultMobilePartyAIModelPatchesTests
{
    [Fact]
    public void StrongerPartyCheckRecognizesAnActivePlayerAndPreservesTheAllyCheck()
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
        Assert.Equal(original[strongerCheckIndex].labels, strongerPartyCheck.labels);
        Assert.Equal(original[strongerCheckIndex].blocks, strongerPartyCheck.blocks);
        var recognizesPlayer = (Func<MobileParty, bool>)((MethodInfo)strongerPartyCheck.operand)
            .CreateDelegate(typeof(Func<MobileParty, bool>));

        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsActive = true;
        Assert.False(recognizesPlayer(party));

        var playerObjects = (ConditionalWeakTable<object, ControlledObjectInfo>)
            AccessTools.Field(typeof(PlayerManager), "PlayerObjects").GetValue(null)!;
        playerObjects.Add(party, new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));
        try
        {
            Assert.True(recognizesPlayer(party));
            party.IsActive = false;
            Assert.False(recognizesPlayer(party));
        }
        finally
        {
            playerObjects.Remove(party);
        }
    }

    [Fact]
    public void ReorderedMainPartyReadsRejectTheAllyCheck()
    {
        const int readAndFollowingInstructions = 5;
        var getter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.IsMainParty));
        var scorer = AccessTools.Method(typeof(DefaultMobilePartyAIModel), nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior));
        var original = PatchProcessor.GetOriginalInstructions(scorer).ToArray();
        int first = Array.FindIndex(original, code => code.Calls(getter));
        int second = Array.FindLastIndex(original, code => code.Calls(getter));
        var reordered = original.ToArray();
        Array.Copy(original, second, reordered, first, readAndFollowingInstructions);
        Array.Copy(original, first, reordered, second, readAndFollowingInstructions);

        var error = Assert.Throws<InvalidOperationException>(
            () => DefaultMobilePartyAIModelPatches.ReplaceStrongerMainPartyCheck(reordered));

        Assert.Contains("guard the stronger-party comparison", error.Message);
    }
}
