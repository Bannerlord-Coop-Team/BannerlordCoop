using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch]
internal class DisableVillageHostileActionCampaignBehavior
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), nameof(VillageHostileActionCampaignBehavior.OnItemsLooted));
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), nameof(VillageHostileActionCampaignBehavior.OnMapEventEnded));
    }

    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
