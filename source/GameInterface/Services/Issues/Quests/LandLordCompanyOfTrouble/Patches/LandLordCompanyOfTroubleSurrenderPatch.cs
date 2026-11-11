using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class LandLordCompanyOfTroubleSurrenderPatch
{
    // Taking the company prisoner or recruiting it destroys it outside a battle, which never ends the quest
    [HarmonyPatch(typeof(BanditInteractionsCampaignBehavior), nameof(BanditInteractionsCampaignBehavior.conversation_bandits_surrender_on_condition))]
    [HarmonyPostfix]
    private static void SurrenderConditionPostfix(ref bool __result)
    {
        if (!__result || !ContainerProvider.TryResolve<ILandLordCompanyOfTroubleService>(out var service))
        {
            return;
        }

        if (service.IsHostileCompany(MobileParty.ConversationParty))
        {
            __result = false;
        }
    }
}
