using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;

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

[HarmonyPatch(typeof(CustomPartyComponent))]
public class CustomPartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CustomPartyComponentPatches>();

    [HarmonyPatch(nameof(CustomPartyComponent._name), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetNamePrefix(CustomPartyComponent __instance, TextObject ____name)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client changed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
            return false;
        }

        var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.Name, ____name.ToString());

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    //[HarmonyPatch(nameof(CustomPartyComponent._homeSettlement), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetHomeSettlementPrefix(CustomPartyComponent __instance, Settlement value)
    //{
    //    // Call original if we called it
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client changed unmanaged {name}\n"
    //            + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
    //        return false;
    //    }

    //    if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
    //    if (objectManager.TryGetId(value, out var id) == false) return true;

    //    var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.HomeSettlement, id);

    //    MessageBroker.Instance.Publish(__instance, message);

    //    return true;
    //}

    //[HarmonyPatch(nameof(CustomPartyComponent._owner), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetOwnerPrefix(CustomPartyComponent __instance, Hero value)
    //{
    //    // Call original if we called it
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client changed unmanaged {name}\n"
    //            + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
    //        return false;
    //    }

    //    if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
    //    if (objectManager.TryGetId(value, out var id) == false) return true;

    //    var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.Owner, id);

    //    MessageBroker.Instance.Publish(__instance, message);

    //    return true;
    //}

    //[HarmonyPatch(nameof(CustomPartyComponent._customPartyBaseSpeed), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetBaseSpeedPrefix(CustomPartyComponent __instance, float value)
    //{
    //    // Call original if we called it
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client changed unmanaged {name}\n"
    //            + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
    //        return false;
    //    }

    //    var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.BaseSpeed, value.ToString());

    //    MessageBroker.Instance.Publish(__instance, message);

    //    return true;
    //}

    //[HarmonyPatch(nameof(CustomPartyComponent._partyMountStringId), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetMountIdPrefix(CustomPartyComponent __instance, string value)
    //{
    //    // Call original if we called it
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client changed unmanaged {name}\n"
    //            + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
    //        return false;
    //    }

    //    var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.MountId, value);

    //    MessageBroker.Instance.Publish(__instance, message);

    //    return true;
    //}

    //[HarmonyPatch(nameof(CustomPartyComponent._partyHarnessStringId), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetHarnessIdPrefix(CustomPartyComponent __instance, string value)
    //{
    //    // Call original if we called it
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client changed unmanaged {name}\n"
    //            + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
    //        return false;
    //    }

    //    var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.HarnessId, value);

    //    MessageBroker.Instance.Publish(__instance, message);

    //    return true;
    //}

    //[HarmonyPatch(nameof(CustomPartyComponent._avoidHostileActions), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetAvoidHostileActionsPrefix(CustomPartyComponent __instance, bool value)
    //{
    //    // Call original if we called it
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client changed unmanaged {name}\n"
    //            + "Callstack: {callstack}", typeof(CustomPartyComponent), Environment.StackTrace);
    //        return false;
    //    }

    //    var message = new CustomPartyComponentUpdated(__instance, CustomPartyComponentType.HarnessId, value.ToString());

    //    MessageBroker.Instance.Publish(__instance, message);

    //    return true;
    //}
}