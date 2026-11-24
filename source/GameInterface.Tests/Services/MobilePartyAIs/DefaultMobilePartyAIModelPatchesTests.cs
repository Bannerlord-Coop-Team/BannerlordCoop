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

        var patched = DefaultMobilePartyAIModelPatches.ReplaceStrongerMainPartyCheck(original).ToList();
        Assert.Same(mainPartyReads[1], Assert.Single(patched, code => code.Calls(getter)));
        var strongerPartyCheck = patched[original.IndexOf(mainPartyReads[0])];
        Assert.Equal(OpCodes.Call, strongerPartyCheck.opcode);
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void UnexpectedMainPartyReadCountReportsDrift(int count)
    {
        var getter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.IsMainParty));
        var instructions = Enumerable.Range(0, count).Select(_ => new CodeInstruction(OpCodes.Callvirt, getter)).ToArray();

        var error = Assert.Throws<InvalidOperationException>(
            () => DefaultMobilePartyAIModelPatches.ReplaceStrongerMainPartyCheck(instructions).ToArray());

        Assert.EndsWith($"found {count}.", error.Message);
        Assert.All(instructions, instruction => Assert.True(instruction.Calls(getter)));
    }
}
