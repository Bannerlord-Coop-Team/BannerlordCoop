using GameInterface.Policies;
using HarmonyLib;
using Common;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Registry.Patches;

[HarmonyPatch(typeof(MBObjectBase))]
internal class MBObjectBasePatches
{
    [HarmonyPatch(nameof(MBObjectBase.StringId), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool StringIdPrefix(MBObjectBase __instance, string value)
    {
        // Call original if we allow this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Quest journals match by StringId, including after an issue detaches its quest.
        // Preserve vanilla's issue-derived quest id on both the server and the owner client.
        if (__instance is QuestBase) return true;

        // IssueManager assigns issue identities on the authoritative server.
        if (__instance is IssueBase && ModInformation.IsServer) return true;

        if (allowedTypes.Contains(__instance.GetType())) return true;

        return false;
    }

    public static HashSet<Type> allowedTypes = new HashSet<Type>()
    {
        typeof(MenuContext),
    };
}
