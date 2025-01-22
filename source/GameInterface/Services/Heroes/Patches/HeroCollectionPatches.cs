using Common.Logging;
using Common.Messaging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using GameInterface.Policies;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Services.Heroes.Messages.Collections;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch]
internal class HeroCollectionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCollectionPatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Hero)))
        {
            yield return method;
        }
        yield return AccessTools.Method(typeof(RecruitmentVM), nameof(RecruitmentVM.OnDone));
        yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), nameof(RecruitmentCampaignBehavior.RecruitVolunteersFromNotable));
        yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), nameof(RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement));
        yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), nameof(RecruitmentCampaignBehavior.ApplyInternal));
        yield return AccessTools.Method(typeof(Town), nameof(Town.DailyGarrisonAdjustment));
        // Carvans
        yield return AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.OnFinalize));
        yield return AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.OnInitialize));
        // Alleys
        yield return AccessTools.Method(typeof(Alley), nameof(Alley.AfterLoad));
        yield return AccessTools.Method(typeof(Alley), nameof(Alley.SetOwner));
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var stack = new Stack<CodeInstruction>();

        var VolunteerArrayType = AccessTools.Field(typeof(Hero), nameof(Hero.VolunteerTypes));
        var arrayAssignIntercept = AccessTools.Method(typeof(HeroCollectionPatches), nameof(ArrayAssignIntercept));
        foreach (var instruction in instructions)
        {
            if (stack.Count > 0 && instruction.opcode == OpCodes.Stelem_Ref)
            {
                stack.Pop();

                var newInstr = new CodeInstruction(OpCodes.Call, arrayAssignIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
                continue;
            }

            if (instruction.opcode == OpCodes.Ldfld && instruction.operand as FieldInfo == VolunteerArrayType)
            {
                stack.Push(instruction);
            }

            yield return instruction;
        }
    }

    public static void ArrayAssignIntercept(CharacterObject[] VolunteerTypes, int index, CharacterObject value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            VolunteerTypes[index] = value;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
            return;
        }
        var message = new VolunteerTypesArrayUpdated(instance, value, index);
        MessageBroker.Instance.Publish(instance, message);

        VolunteerTypes[index] = value;
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ChildrenTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var childrenAddMethod = typeof(MBList<Hero>).GetMethod("Add");
        var childrenAddIntercept = AccessTools.Method(typeof(HeroCollectionPatches), nameof(ChildrenAddIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == childrenAddMethod)
            {
                var newInstr = new CodeInstruction(OpCodes.Call, childrenAddIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void ChildrenAddIntercept(MBList<Hero> children, Hero value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            children.Add(value);
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
            return;
        }
        var message = new ChildrenListUpdated(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        children.Add(value);
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> CaravanTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var addMethod = typeof(List<CaravanPartyComponent>).GetMethod("Add");
        var caravanAddIntercept = AccessTools.Method(typeof(HeroCollectionPatches), nameof(CaravanAddIntercept));

        var removeMethod = typeof(List<CaravanPartyComponent>).GetMethod("Remove");
        var removeIntercept = typeof(HeroCollectionPatches).GetMethod(nameof(CaravanRemoveIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == addMethod)
            {

                var newInstr = new CodeInstruction(OpCodes.Call, caravanAddIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            }
            else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == removeMethod)
            {
                var newInstr = new CodeInstruction(OpCodes.Call, removeIntercept);
                //newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            } 
            else
            {
                yield return instruction;
            }
        }
    }

    public static void CaravanAddIntercept(List<CaravanPartyComponent> ownedCaravans, CaravanPartyComponent value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            ownedCaravans.Add(value);
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
            return;
        }
        var message = new CaravanListUpdated(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        ownedCaravans.Add(value);
    }

    public static bool CaravanRemoveIntercept(List<CaravanPartyComponent> ownedCaravans, CaravanPartyComponent value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return ownedCaravans.Remove(value);
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Equipment), Environment.StackTrace);
            return ownedCaravans.Remove(value);
        }
        var message = new CaravanListRemoved(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        return ownedCaravans.Remove(value);
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> AlleyTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var addMethod = typeof(List<Alley>).GetMethod("Add");
        var addIntercept = AccessTools.Method(typeof(HeroCollectionPatches), nameof(AlleyAddIntercept));

        var removeMethod = typeof(List<Alley>).GetMethod("Remove");
        var removeIntercept = typeof(HeroCollectionPatches).GetMethod(nameof(AlleyRemoveIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == addMethod)
            {
                var newInstr = new CodeInstruction(OpCodes.Call, addIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            }
            else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == removeMethod) 
            {
                var newInstr = new CodeInstruction(OpCodes.Call, removeIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void AlleyAddIntercept(List<Alley> ownedAlleys, Alley value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            ownedAlleys.Add(value);
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return;
        }
        var message = new AlleyListUpdated(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        ownedAlleys.Add(value);
    }

    public static bool AlleyRemoveIntercept(List<Alley> ownedAlleys, Alley value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return ownedAlleys.Remove(value);
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return ownedAlleys.Remove(value);
        }
        var message = new AlleyListRemoved(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        return ownedAlleys.Remove(value);
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> WorkshopTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var stack = new Stack<CodeInstruction>();

        var addMethod = typeof(MBList<Workshop>).GetMethod("Add");
        var addIntercept = AccessTools.Method(typeof(HeroCollectionPatches), nameof(WorkshopAddIntercept));

        var removeMethod = typeof(MBList<Workshop>).GetMethod("Remove");
        var removeIntercept = typeof(HeroCollectionPatches).GetMethod(nameof(WorkshopRemoveIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == addMethod)
            {
                var newInstr = new CodeInstruction(OpCodes.Call, addIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            }
            else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == removeMethod)
            {
                var newInstr = new CodeInstruction(OpCodes.Call, removeIntercept);
                newInstr.labels = instruction.labels;

                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return newInstr;
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void WorkshopAddIntercept(MBList<Workshop> ownedWorkshops, Workshop value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            ownedWorkshops.Add(value);
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return;
        }
        var message = new WorkshopListUpdated(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        ownedWorkshops.Add(value);
    }

    public static bool WorkshopRemoveIntercept(MBList<Workshop> ownedWorkshops, Workshop value, Hero instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return ownedWorkshops.Remove(value);
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return ownedWorkshops.Remove(value);
        }
        var message = new WorkshopListRemoved(instance, value);
        MessageBroker.Instance.Publish(instance, message);

        return ownedWorkshops.Remove(value);
    }
}


