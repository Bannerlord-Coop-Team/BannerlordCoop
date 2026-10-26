using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleCompanyDestroyedPatch
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    private static void OnMobilePartyDestroyedPostfix(MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (!ContainerProvider.TryResolve<ILandLordCompanyOfTroubleService>(out var service))
        {
            return;
        }

        service.OnMobilePartyDestroyed(mobileParty, destroyerParty);
    }
}
