using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;


[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class DisableRecruitmentCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    static bool PrefixRegisterEvents() => ModInformation.IsServer;

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.CheckRecruiting))]
    [HarmonyPrefix]
    /// Only allow recruiting for AI parties
    static bool PrefixCheckRecruiting(MobileParty mobileParty) => !mobileParty.IsPlayer();
}
