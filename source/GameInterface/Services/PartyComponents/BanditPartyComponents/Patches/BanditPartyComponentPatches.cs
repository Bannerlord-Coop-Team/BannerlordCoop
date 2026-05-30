using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.BanditPartyComponents.Messages;
using GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents.Patches;

public enum BanditPartyComponentType
{
    Hideout,
    IsBossParty,
}

[HarmonyPatch(typeof(BanditPartyComponent))]
public class BanditPartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentPatches>();

    [HarmonyPatch(nameof(BanditPartyComponent.IsBossParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsBossPartyPrefix(BanditPartyComponent __instance, bool value)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client changed unmanaged {name}", typeof(BanditPartyComponent));
            return false;
        }

        var message = new BanditPartyComponentUpdated(__instance, BanditPartyComponentType.IsBossParty, value.ToString());

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(BanditPartyComponent.Hideout), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetHideoutPrefix(BanditPartyComponent __instance, Hideout value)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client changed unmanaged {name}", typeof(BanditPartyComponent));
            return false;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
        if (objectManager.TryGetId(value, out var hideoutId) == false) return true;

        var message = new BanditPartyComponentUpdated(__instance, BanditPartyComponentType.Hideout, hideoutId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}

[HarmonyPatch(typeof(BanditPartyComponent))]
public class BanditPartyComponentTranspilers
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentTranspilers>();

    //public static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(BanditPartyComponent));


    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> InitializationArgsTranspiler(IEnumerable<CodeInstruction> instructions)
    //{
    //    var field = AccessTools.Field(typeof(BanditPartyComponent), nameof(BanditPartyComponent._initializationArgs));
    //    var fieldIntercept = AccessTools.Method(typeof(BanditPartyComponentTranspilers), nameof(InitializationArgsIntercept));

    //    foreach (var instruction in instructions)
    //    {
    //        if (instruction.StoresField(field))
    //        {
    //            yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
    //        }
    //        else
    //        {
    //            yield return instruction;
    //        }
    //    }
    //}
    [HarmonyPatch("OnMobilePartySetOnCreation")]
    [HarmonyPrefix]
    private static bool OnMobilePartySetOnCreation_Prefix(BanditPartyComponent __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return true;
        if (__instance.MobileParty == null) return true;

        var message = new BanditPartyComponentInitArgsUpdated(__instance, __instance._initializationArgs);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
    //public static void InitializationArgsIntercept(BanditPartyComponent instance, BanditPartyComponent.InitializationArgs initArgs)
    //{
    //    if (CallOriginalPolicy.IsOriginalAllowed())
    //    {
    //        instance._initializationArgs = initArgs;
    //        return;
    //    }

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client updated managed {type}", nameof(instance._initializationArgs));
    //        return;
    //    }

    //    var message = new BanditPartyComponentInitArgsUpdated(instance, initArgs);
    //    MessageBroker.Instance.Publish(instance, message);

    //    instance._initializationArgs = initArgs;
    //}
}