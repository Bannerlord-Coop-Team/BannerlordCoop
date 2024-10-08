using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="MobileParty"/> objects.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class PartyLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroLifetimePatches>();


    [HarmonyPatch(nameof(MobileParty.RemoveParty))]
    [HarmonyPrefix]
    private static bool RemoveParty_Prefix(ref MobileParty __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new PartyDestroyed(__instance));

        return true;
    }

    /// Disable setting of string id in <see cref="MobileParty.CreateParty"/> so we can manage the id on our own
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.CreateParty))]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var set_stringId = AccessTools.PropertySetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId));

        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Callvirt && instr.operand as MethodInfo == set_stringId)
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Pop);
            }
            else
            {
                yield return instr;
            }
        }
    }
}

/// <summary>
/// Patches for lifecycle of <see cref="MobileParty"/> objects.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class PartyCtorPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredConstructors(typeof(MobileParty));
    }

    [HarmonyPrefix]
    private static void Prefix(ref MobileParty __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            __instance.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>("COOP_PARTY");
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);

            __instance.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<MobileParty>("ERROR_PARTY");

            return;
        }

        MessageBroker.Instance.Publish(__instance, new PartyCreated(__instance));

        return;
    }
}