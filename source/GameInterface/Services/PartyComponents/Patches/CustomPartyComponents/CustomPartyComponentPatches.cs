using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;

[HarmonyPatch]
public class CustomPartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CustomPartyComponentPatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(CustomPartyComponent)))
        {
            yield return method;
        }
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> NameTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._name));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(NameIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void NameIntercept(CustomPartyComponent instance, TextObject newName)
    {
        // Change instance for all branches (prevent crashing)
        instance._name = newName;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.Name, newName.ToString());
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HomeSettlementTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._homeSettlement));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(HomeSettlementIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void HomeSettlementIntercept(CustomPartyComponent instance, Settlement newSettlement)
    {
        if (instance._homeSettlement == newSettlement) return;

        // Change instance for all branches (prevent crashing)
        instance._homeSettlement = newSettlement;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;
        if (objectManager.TryGetId(newSettlement, out var id) == false) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.HomeSettlement, id);
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OwnerTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._owner));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(OwnerIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void OwnerIntercept(CustomPartyComponent instance, Hero newOwner)
    {
        // Change instance for all branches (prevent crashing)
        instance._owner = newOwner;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;
        if (objectManager.TryGetId(newOwner, out var id) == false) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.Owner, id);
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> BaseSpeedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._customPartyBaseSpeed));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(BaseSpeedIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void BaseSpeedIntercept(CustomPartyComponent instance, float newSpeed)
    {
        // Change instance for all branches (prevent crashing)
        instance._customPartyBaseSpeed = newSpeed;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.BaseSpeed, newSpeed.ToString());
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MountIdTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._partyMountStringId));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(MountIdIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void MountIdIntercept(CustomPartyComponent instance, string newId)
    {
        // Change instance for all branches (prevent crashing)
        instance._partyMountStringId = newId;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.MountId, newId);
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HarnessIdTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._partyHarnessStringId));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(HarnessIdIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void HarnessIdIntercept(CustomPartyComponent instance, string newId)
    {
        // Change instance for all branches (prevent crashing)
        instance._partyHarnessStringId = newId;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.HarnessId, newId);
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AvoidHostileActionsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._avoidHostileActions));
        var fieldIntercept = AccessTools.Method(typeof(CustomPartyComponentPatches), nameof(AvoidHostileActionsIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void AvoidHostileActionsIntercept(CustomPartyComponent instance, bool value)
    {
        // Change instance for all branches (prevent crashing)
        instance._avoidHostileActions = value;

        if (CallPolicy.IsOriginalAllowed()) return;
        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.AvoidHostileActions, value.ToString());
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(instance, message);
    }
}

public enum CustomPartyComponentType
{
    Name,
    HomeSettlement,
    Owner,
    BaseSpeed,
    MountId,
    HarnessId,
    AvoidHostileActions
}