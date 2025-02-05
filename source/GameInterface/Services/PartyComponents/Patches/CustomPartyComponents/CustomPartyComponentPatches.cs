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
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._name = newName;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.Name, newName.ToString());
        MessageBroker.Instance.Publish(instance, message);

        instance._name = newName;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._homeSettlement = newSettlement;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;
        if (objectManager.TryGetId(newSettlement, out var id) == false) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.HomeSettlement, id);
        MessageBroker.Instance.Publish(instance, message);

        instance._homeSettlement = newSettlement;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._owner = newOwner;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;
        if (objectManager.TryGetId(newOwner, out var id) == false) return;

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.Owner, id);
        MessageBroker.Instance.Publish(instance, message);

        instance._owner = newOwner;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._customPartyBaseSpeed = newSpeed;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.BaseSpeed, newSpeed.ToString());
        MessageBroker.Instance.Publish(instance, message);

        instance._customPartyBaseSpeed = newSpeed;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyMountStringId = newId;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.MountId, newId);
        MessageBroker.Instance.Publish(instance, message);

        instance._partyMountStringId = newId;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyHarnessStringId = newId;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.HarnessId, newId);
        MessageBroker.Instance.Publish(instance, message);

        instance._partyHarnessStringId = newId;
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
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._avoidHostileActions = value;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            return;
        }

        var message = new CustomPartyComponentUpdated(instance, CustomPartyComponentType.AvoidHostileActions, value.ToString());
        MessageBroker.Instance.Publish(instance, message);

        instance._avoidHostileActions = value;
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