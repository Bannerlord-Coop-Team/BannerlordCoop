using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch]
internal class DefaultKingdomDecisionPermissionModelPatches
{
    [HarmonyPatch(typeof(DefaultKingdomDecisionPermissionModel), nameof(DefaultKingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms))]
    [HarmonyPrefix]
    private static bool Prefix(DefaultKingdomDecisionPermissionModel __instance, Kingdom kingdom1, Kingdom kingdom2, ref TextObject reason, ref bool __result)
    {
        reason = null;
        Kingdom callingKingdom = null;
        if (Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(kingdom1, kingdom2))
        {
            reason = new TextObject("{=eNPupZOp}These kingdoms can not declare peace at this time.", null);
            __result = false;
            return false;
        }
        IAllianceCampaignBehavior allianceCampaignBehavior = __instance.AllianceCampaignBehavior;
        if (allianceCampaignBehavior != null && allianceCampaignBehavior.IsAtWarByCallToWarAgreement(kingdom1, kingdom2, out callingKingdom))
        {
            reason = __instance.GetExplanationForPeaceOfferWithCallToWar(callingKingdom, kingdom1, kingdom2);
            __result = false;
            return false;
        }
        IAllianceCampaignBehavior allianceCampaignBehavior2 = __instance.AllianceCampaignBehavior;
        if (allianceCampaignBehavior2 != null && allianceCampaignBehavior2.IsAtWarByCallToWarAgreement(kingdom2, kingdom1, out callingKingdom))
        {
            reason = __instance.GetExplanationForPeaceOfferWithCallToWar(callingKingdom, kingdom2, kingdom1);
            __result = false;
            return false;
        }
        if (kingdom1.RulingClan.IsPlayerClan() && kingdom2.RulingClan.IsPlayerClan()) // checks if both of the kingdoms ruler clan are playerclans
        {
            __result = true;
            return false;
        }

        if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(kingdom1, kingdom2))
        {
            reason = new TextObject("{=JkQ7fmcX}The enemy is not open to negotiations.", null);
            __result = false;
            return false;
        }
        __result = true;
        return false;
    }
}
