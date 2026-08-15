using GameInterface.Services.Clans.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

public class ClanPartyItemVMPatchesTests
{
    [Fact]
    public void UpdatePropertiesTranspiler_ClanLeaderLookup_PreservesPartyLeaderForNullFallback()
    {
        var clanGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Clan));
        var clanLeaderGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.Leader));
        var instructions = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Callvirt, clanGetter),
            new(OpCodes.Callvirt, clanLeaderGetter),
            new(OpCodes.Pop),
            new(OpCodes.Ret),
        };
        ILGenerator generator = new DynamicMethod("Test", typeof(void), Type.EmptyTypes).GetILGenerator();

        List<CodeInstruction> result = ClanPartyItemVMPatches.UpdatePropertiesTranspiler(instructions, generator).ToList();

        Assert.Equal(OpCodes.Ldarg_0, result[0].opcode);
        Assert.Equal(OpCodes.Dup, result[1].opcode);
        Assert.True(result[2].Calls(clanGetter));
        Assert.Contains(result, instruction => instruction.Calls(clanLeaderGetter));
        Assert.Equal(2, result.Count(instruction => instruction.opcode == OpCodes.Brtrue_S));
        Assert.Equal(OpCodes.Ret, result[^1].opcode);

        List<CodeInstruction> secondResult = ClanPartyItemVMPatches.UpdatePropertiesTranspiler(result, generator).ToList();

        Assert.Equal(result, secondResult);
    }

    [Fact]
    public void UpdatePropertiesTranspiler_UnexpectedMethodShape_Throws()
    {
        var instructions = new[] { new CodeInstruction(OpCodes.Ret) };
        ILGenerator generator = new DynamicMethod("Test", typeof(void), Type.EmptyTypes).GetILGenerator();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClanPartyItemVMPatches.UpdatePropertiesTranspiler(instructions, generator).ToList());

        Assert.Contains("found 0", exception.Message);
    }

    [Fact]
    public void UpdatePropertiesTranspiler_DuplicateGameAssemblyIdentity_ExecutesClanlessFallback()
    {
        AssemblyName assemblyName = new($"ForeignCampaignSystem.{Guid.NewGuid()}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name!);
        TypeBuilder foreignHero = module.DefineType(typeof(Hero).FullName!, TypeAttributes.Public);
        TypeBuilder foreignClan = module.DefineType(typeof(Clan).FullName!, TypeAttributes.Public);
        FieldBuilder clanField = foreignHero.DefineField("ClanValue", foreignClan, FieldAttributes.Public);
        FieldBuilder leaderField = foreignClan.DefineField("LeaderValue", foreignHero, FieldAttributes.Public);
        DefineGetter(foreignHero, nameof(Hero.Clan), foreignClan, clanField);
        DefineGetter(foreignClan, nameof(Clan.Leader), foreignHero, leaderField);
        Type heroType = foreignHero.CreateType()!;
        Type clanType = foreignClan.CreateType()!;
        MethodInfo foreignClanGetter = heroType.GetMethod($"get_{nameof(Hero.Clan)}")!;
        MethodInfo foreignClanLeaderGetter = clanType.GetMethod($"get_{nameof(Clan.Leader)}")!;
        DynamicMethod getLeader = new("GetLeader", heroType, new[] { heroType });
        ILGenerator generator = getLeader.GetILGenerator();
        var instructions = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Callvirt, foreignClanGetter),
            new CodeInstruction(OpCodes.Callvirt, foreignClanLeaderGetter),
            new CodeInstruction(OpCodes.Ret),
        };
        List<CodeInstruction> result = ClanPartyItemVMPatches.UpdatePropertiesTranspiler(instructions, generator).ToList();
        Emit(generator, result);
        object partyLeader = Activator.CreateInstance(heroType)!;

        object clanlessResult = getLeader.Invoke(null, new[] { partyLeader })!;

        Assert.Same(partyLeader, clanlessResult);

        object clan = Activator.CreateInstance(clanType)!;
        heroType.GetField(clanField.Name)!.SetValue(partyLeader, clan);

        object leaderlessClanResult = getLeader.Invoke(null, new[] { partyLeader })!;

        Assert.Same(partyLeader, leaderlessClanResult);

        object clanLeader = Activator.CreateInstance(heroType)!;
        clanType.GetField(leaderField.Name)!.SetValue(clan, clanLeader);

        object clanLeaderResult = getLeader.Invoke(null, new[] { partyLeader })!;

        Assert.Same(clanLeader, clanLeaderResult);
    }

    private static void DefineGetter(TypeBuilder type, string propertyName, Type returnType, FieldBuilder field)
    {
        MethodBuilder getter = type.DefineMethod(
            $"get_{propertyName}",
            MethodAttributes.Public,
            returnType,
            Type.EmptyTypes);
        ILGenerator il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
    }

    private static void Emit(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            foreach (Label label in instruction.labels)
                generator.MarkLabel(label);

            if (instruction.operand is MethodInfo method)
                generator.Emit(instruction.opcode, method);
            else if (instruction.operand is Label label)
                generator.Emit(instruction.opcode, label);
            else if (instruction.operand is LocalBuilder local)
                generator.Emit(instruction.opcode, local);
            else
                generator.Emit(instruction.opcode);
        }
    }
}
