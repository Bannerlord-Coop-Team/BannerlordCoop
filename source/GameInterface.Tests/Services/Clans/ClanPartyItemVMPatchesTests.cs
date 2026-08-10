using Common.Util;
using GameInterface.Services.Clans.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

public class ClanPartyItemVMPatchesTests
{
    [Fact]
    public void GetClanLeaderOrSelf_ClanlessPartyLeader_ReturnsPartyLeader()
    {
        var partyLeader = ObjectHelper.SkipConstructor<Hero>();

        Hero result = ClanPartyItemVMPatches.GetClanLeaderOrSelf(partyLeader, null!);

        Assert.Same(partyLeader, result);
    }

    [Fact]
    public void UpdatePropertiesTranspiler_ClanLeaderLookup_PreservesPartyLeaderForNullFallback()
    {
        var clanGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Clan));
        var clanLeaderGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.Leader));
        var clanLeaderOrSelf = AccessTools.Method(typeof(ClanPartyItemVMPatches), nameof(ClanPartyItemVMPatches.GetClanLeaderOrSelf));
        var instructions = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Callvirt, clanGetter),
            new(OpCodes.Callvirt, clanLeaderGetter),
            new(OpCodes.Pop),
            new(OpCodes.Ret),
        };

        List<CodeInstruction> result = ClanPartyItemVMPatches.UpdatePropertiesTranspiler(instructions).ToList();

        Assert.Collection(
            result,
            instruction => Assert.Equal(OpCodes.Ldarg_0, instruction.opcode),
            instruction => Assert.Equal(OpCodes.Dup, instruction.opcode),
            instruction => Assert.True(instruction.Calls(clanGetter)),
            instruction =>
            {
                Assert.Equal(OpCodes.Call, instruction.opcode);
                Assert.Equal(clanLeaderOrSelf, instruction.operand);
            },
            instruction => Assert.Equal(OpCodes.Pop, instruction.opcode),
            instruction => Assert.Equal(OpCodes.Ret, instruction.opcode));
    }

    [Fact]
    public void UpdatePropertiesTranspiler_UnexpectedMethodShape_Throws()
    {
        var instructions = new[] { new CodeInstruction(OpCodes.Ret) };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClanPartyItemVMPatches.UpdatePropertiesTranspiler(instructions).ToList());

        Assert.Contains("found 0", exception.Message);
    }
}
