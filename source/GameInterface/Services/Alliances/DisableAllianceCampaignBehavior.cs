using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Alliances;

[HarmonyPatch(typeof(AllianceCampaignBehavior))]
internal class DisableAllianceCampaignBehavior
{
    [HarmonyPatch(nameof(AllianceCampaignBehavior.RegisterEvents))]
    static bool RegisterEventsPrefix() => true;

    // Disable these methods on the client
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
            AccessTools.Method(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.DailyTickClan)),
            AccessTools.Method(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.OnWarDeclared)),
            AccessTools.Method(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.OnMakePeace)),
            AccessTools.Method(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.OnKingdomDestroyed)),
            AccessTools.Method(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.OnGameLoadFinished))
    };

    static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
