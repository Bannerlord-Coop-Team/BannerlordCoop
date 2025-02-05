using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.Army;
using static TaleWorlds.CampaignSystem.CampaignTime;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patches required for creating an Army
/// </summary>
[HarmonyPatch]
internal class ArmyLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    [HarmonyPatch(typeof(Army), MethodType.Constructor, typeof(Kingdom), typeof(MobileParty), typeof(ArmyTypes))]
    [HarmonyPrefix]
    private static bool CreateArmyPrefix(ref Army __instance, Kingdom kingdom, MobileParty leaderParty, ArmyTypes armyType)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Army), Environment.StackTrace);
            return true;
        }

        
        var message = new ArmyCreated(__instance, kingdom, leaderParty, armyType);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(typeof(DisbandArmyAction), "ApplyInternal")]
    [HarmonyPrefix]
    public static bool DisperseInternal()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Army), Environment.StackTrace);
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(Army), "DisperseInternal")]
    [HarmonyPostfix]
    public static void DisbandArmyPostfix(Army __instance, Army.ArmyDispersionReason reason)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return;
        }

        var data = new ArmyDestructionData(__instance, reason);
        var message = new ArmyDestroyed(data, __instance);

        MessageBroker.Instance.Publish(__instance, message);
    }

    public static void OverrideDestroyArmy(Army army, ArmyDispersionReason reason)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                army.DisperseInternal(reason);
            }
        });
    }
}
