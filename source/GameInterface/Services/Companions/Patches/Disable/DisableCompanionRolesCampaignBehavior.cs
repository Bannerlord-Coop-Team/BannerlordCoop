using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches.Disable
{
    [HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
    
    internal class DisableCompanionRolesCampaignBehavior
    {
        [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.RegisterEvents))]
        static bool RegisterEventsPrefix() => true;

        // Disable these methods on the client
        private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
        {
            AccessTools.Method(typeof(CompanionRolesCampaignBehavior), nameof(CompanionRolesCampaignBehavior.OnHeroRelationChanged)),
            AccessTools.Method(typeof(CompanionRolesCampaignBehavior), nameof(CompanionRolesCampaignBehavior.OnCompanionRemoved))
        };

        static bool Prefix()
        {
            return ModInformation.IsServer;
        }
    }
}