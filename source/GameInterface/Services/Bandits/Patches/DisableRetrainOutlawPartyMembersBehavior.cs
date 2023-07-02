using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(RetrainOutlawPartyMembersBehavior))]
internal class DisableRetrainOutlawPartyMembersBehavior
{
    [HarmonyPatch(nameof(RetrainOutlawPartyMembersBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
