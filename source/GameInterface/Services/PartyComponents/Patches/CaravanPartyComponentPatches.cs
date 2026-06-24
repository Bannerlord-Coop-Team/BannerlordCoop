using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(CaravanPartyComponent))]
internal class CaravanPartyComponentPatches
{
    [HarmonyPatch(nameof(CaravanPartyComponent.GetDefaultComponentBanner))]
    [HarmonyPrefix]
    public static bool GetDefaultComponentBannerPrefix(ref CaravanPartyComponent __instance, ref Banner __result)
    {
        if (__instance.Leader != null)
        {
            __result = __instance.Leader.ClanBanner;
        }
        else if (__instance.Owner.IsPlayerHero()) // Replace Hero.MainHero with IsPlayerHero()
        {
            __result = __instance.Owner.MapFaction.Banner;
        }
        else
        {
            __result = __instance.Owner.HomeSettlement.OwnerClan.MapFaction.Banner;
        }

        return false;
    }
}

[HarmonyPatch(typeof(CaravanPartyComponent))]
public class CaravanPartyComponentTranspilers
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanPartyComponentTranspilers>();

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(CaravanPartyComponent)))
            yield return ctor;

        // ChangeHomeSettlement also assigns Settlement
        yield return AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.ChangeHomeSettlement));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var initArgsField = AccessTools.Field(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent._initializationArgs));
        var initArgsIntercept = AccessTools.Method(typeof(CaravanPartyComponentTranspilers), nameof(InitializationArgsIntercept));

        var ownerSetter = AccessTools.PropertySetter(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Owner));
        var ownerIntercept = AccessTools.Method(typeof(CaravanPartyComponentTranspilers), nameof(OwnerSetIntercept));

        var settlementSetter = AccessTools.PropertySetter(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Settlement));
        var settlementIntercept = AccessTools.Method(typeof(CaravanPartyComponentTranspilers), nameof(SettlementSetIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(initArgsField))
            {
                yield return new CodeInstruction(OpCodes.Call, initArgsIntercept);
            }
            else if (instruction.Calls(ownerSetter))
            {
                yield return new CodeInstruction(OpCodes.Call, ownerIntercept);
            }
            else if (instruction.Calls(settlementSetter))
            {
                yield return new CodeInstruction(OpCodes.Call, settlementIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void OwnerSetIntercept(CaravanPartyComponent instance, Hero owner)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.Owner = owner;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {type}", "CaravanPartyComponent.Owner");
            instance.Owner = owner;
            return;
        }

        MessageBroker.Instance.Publish(instance, new CaravanPartyOwnerChanged(instance, owner));

        instance.Owner = owner;
    }

    public static void SettlementSetIntercept(CaravanPartyComponent instance, Settlement settlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.Settlement = settlement;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {type}", "CaravanPartyComponent.Settlement");
            instance.Settlement = settlement;
            return;
        }

        MessageBroker.Instance.Publish(instance, new CaravanPartySettlementChanged(instance, settlement));

        instance.Settlement = settlement;
    }

    public static void InitializationArgsIntercept(CaravanPartyComponent instance, CaravanPartyComponent.InitializationArgs initArgs)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._initializationArgs = initArgs;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {type}", nameof(instance._initializationArgs));
            instance._initializationArgs = initArgs;
            return;
        }

        var message = new CaravanPartyComponentInitArgsUpdated(instance, initArgs);
        MessageBroker.Instance.Publish(instance, message);

        instance._initializationArgs = initArgs;
    }
}

[HarmonyPatch(typeof(CaravanPartyComponent.InitializationArgs))]
internal class CaravanPartyComponentInitializationArgsPatches
{
    [HarmonyPatch(nameof(CaravanPartyComponent.InitializationArgs.InitializeCaravanOnCreation))]
    [HarmonyPrefix]
    public static bool InitializeLordPartyPropertiesPrefix(ref LordPartyComponent.InitializationArgs __instance, MobileParty mobileParty, Settlement settlement)
    {
        // Shouldn't be needed?
        return true;
    }
}
