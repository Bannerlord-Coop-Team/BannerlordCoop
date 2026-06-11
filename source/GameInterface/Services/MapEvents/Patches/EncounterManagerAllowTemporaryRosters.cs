using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class EncounterManagerAllowTemporaryRosters
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.PropertyGetter(typeof(PlayerEncounter), nameof(PlayerEncounter.RosterToReceiveLootItems)),
        AccessTools.PropertyGetter(typeof(PlayerEncounter), nameof(PlayerEncounter.RosterToReceiveLootPrisoners)),
        AccessTools.PropertyGetter(typeof(PlayerEncounter), nameof(PlayerEncounter.RosterToReceiveLootMembers)),
    };

    public static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    public static void Postfix()
    {
        AllowedThread.RevokeThisThread();
    }
}
