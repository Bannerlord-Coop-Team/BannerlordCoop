using GameInterface.Services.Clans.Patches;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

public class ClanMembersVMPatchesTests
{
    [Fact]
    public void InstalledClanScreen_UsesCoopClanMemberFactory()
    {
        var constructor = AccessTools.Constructor(typeof(ClanManagementVM), new[]
        {
            typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action),
        });
        var instructions = PatchProcessor.GetOriginalInstructions(constructor);

        var result = ClanMembersVMPatches.ConstructorTranspiler(instructions).ToList();

        Assert.Single(result, instruction => instruction.opcode == OpCodes.Call &&
            instruction.operand is MethodInfo method && method.DeclaringType == typeof(ClanMembersVMPatches));
        Assert.DoesNotContain(result, instruction => instruction.opcode == OpCodes.Newobj &&
            instruction.operand is ConstructorInfo created && created.DeclaringType == typeof(ClanMembersVM));
    }
}
