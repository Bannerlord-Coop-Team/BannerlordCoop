using GameInterface.Services.Clans.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

public class ClanRoleItemVMPatchesTests
{
    private const string HeroTypeName = "TaleWorlds.CampaignSystem.Hero";
    private const string PartyMemberTypeName = "TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanPartyMemberItemVM";
    private const string RoleMemberTypeName = "TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanRoleMemberItemVM";

    [Fact]
    public void RefreshTranspiler_MemberHeroLookup_IsNullSafe()
    {
        (Type _, Type partyMemberType, Type roleMemberType) = CreateForeignTypes();
        MethodInfo memberGetter = roleMemberType.GetMethod("get_Member")!;
        MethodInfo heroGetter = partyMemberType.GetMethod("get_HeroObject")!;
        ILGenerator generator = new DynamicMethod("Test", typeof(void), Type.EmptyTypes).GetILGenerator();
        LocalBuilder roleMember = generator.DeclareLocal(roleMemberType);
        LocalBuilder effectiveOwner = generator.DeclareLocal(heroGetter.ReturnType);
        var instructions = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Callvirt, memberGetter),
            new(OpCodes.Callvirt, heroGetter),
            new(OpCodes.Pop),
            new(OpCodes.Ldloc, roleMember),
            new(OpCodes.Callvirt, memberGetter),
            new(OpCodes.Callvirt, heroGetter),
            new(OpCodes.Ldloc, effectiveOwner),
            new(OpCodes.Pop),
            new(OpCodes.Pop),
            new(OpCodes.Ret),
        };

        List<CodeInstruction> result = ClanRoleItemVMPatches.RefreshTranspiler(instructions, generator).ToList();

        Assert.Equal(OpCodes.Callvirt, result[1].opcode);
        Assert.True(result[2].Calls(heroGetter));
        Assert.Equal(OpCodes.Pop, result[3].opcode);
        Assert.Equal(OpCodes.Ldloc, result[4].opcode);
        Assert.Equal(OpCodes.Callvirt, result[5].opcode);
        Assert.Equal(OpCodes.Dup, result[6].opcode);
        Assert.Equal(OpCodes.Brtrue_S, result[7].opcode);
        Assert.Equal(OpCodes.Pop, result[8].opcode);
        Assert.Equal(OpCodes.Ldnull, result[9].opcode);
        Assert.Equal(OpCodes.Br_S, result[10].opcode);
        Assert.True(result[11].Calls(heroGetter));
        Assert.Equal(OpCodes.Nop, result[12].opcode);
        Assert.Equal(2, result.Count(instruction => instruction.Calls(heroGetter)));

        List<CodeInstruction> secondResult = ClanRoleItemVMPatches.RefreshTranspiler(result, generator).ToList();

        Assert.Equal(result, secondResult);
    }

    [Fact]
    public void RefreshTranspiler_UnexpectedMethodShape_Throws()
    {
        var instructions = new[] { new CodeInstruction(OpCodes.Ret) };
        ILGenerator generator = new DynamicMethod("Test", typeof(void), Type.EmptyTypes).GetILGenerator();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClanRoleItemVMPatches.RefreshTranspiler(instructions, generator).ToList());

        Assert.Contains("found 0", exception.Message);
    }

    [Fact]
    public void RefreshTranspiler_DuplicateGameAssemblyIdentity_ReturnsNullForNotAssignedMember()
    {
        (Type heroType, Type partyMemberType, Type roleMemberType) = CreateForeignTypes();
        MethodInfo foreignMemberGetter = roleMemberType.GetMethod("get_Member")!;
        MethodInfo foreignHeroGetter = partyMemberType.GetMethod("get_HeroObject")!;
        DynamicMethod getHero = new("GetHero", heroType, new[] { roleMemberType });
        ILGenerator generator = getHero.GetILGenerator();
        LocalBuilder roleMemberLocal = generator.DeclareLocal(roleMemberType);
        LocalBuilder effectiveOwnerLocal = generator.DeclareLocal(heroType);
        var instructions = new List<CodeInstruction>
        {
            new(OpCodes.Ldnull),
            new(OpCodes.Stloc, effectiveOwnerLocal),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Stloc, roleMemberLocal),
            new(OpCodes.Ldloc, roleMemberLocal),
            new(OpCodes.Callvirt, foreignMemberGetter),
            new(OpCodes.Callvirt, foreignHeroGetter),
            new(OpCodes.Ldloc, effectiveOwnerLocal),
            new(OpCodes.Pop),
            new(OpCodes.Ret),
        };
        List<CodeInstruction> result = ClanRoleItemVMPatches.RefreshTranspiler(instructions, generator).ToList();
        Emit(generator, result);
        object roleMember = Activator.CreateInstance(roleMemberType)!;

        object nullResult = getHero.Invoke(null, new[] { roleMember });

        Assert.Null(nullResult);

        object partyMember = Activator.CreateInstance(partyMemberType)!;
        object hero = Activator.CreateInstance(heroType)!;
        partyMemberType.GetField("HeroValue")!.SetValue(partyMember, hero);
        roleMemberType.GetField("MemberValue")!.SetValue(roleMember, partyMember);

        object assignedResult = getHero.Invoke(null, new[] { roleMember })!;

        Assert.Same(hero, assignedResult);
    }

    private static (Type Hero, Type PartyMember, Type RoleMember) CreateForeignTypes()
    {
        AssemblyName assemblyName = new($"ForeignClanRole.{Guid.NewGuid()}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name!);
        TypeBuilder foreignHero = module.DefineType(HeroTypeName, TypeAttributes.Public);
        TypeBuilder foreignPartyMember = module.DefineType(PartyMemberTypeName, TypeAttributes.Public);
        TypeBuilder foreignRoleMember = module.DefineType(RoleMemberTypeName, TypeAttributes.Public);
        FieldBuilder heroField = foreignPartyMember.DefineField("HeroValue", foreignHero, FieldAttributes.Public);
        FieldBuilder memberField = foreignRoleMember.DefineField("MemberValue", foreignPartyMember, FieldAttributes.Public);
        DefineGetter(foreignPartyMember, "HeroObject", foreignHero, heroField);
        DefineGetter(foreignRoleMember, "Member", foreignPartyMember, memberField);
        Type heroType = foreignHero.CreateType()!;
        Type partyMemberType = foreignPartyMember.CreateType()!;
        Type roleMemberType = foreignRoleMember.CreateType()!;
        return (heroType, partyMemberType, roleMemberType);
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
