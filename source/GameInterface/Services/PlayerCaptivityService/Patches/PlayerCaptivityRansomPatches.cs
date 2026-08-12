using Common;
using GameInterface.Services.PlayerCaptivityService.Handlers;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

/// <summary>
/// Replaces the unsynchronized local ransom roll at the presentation boundary with the same
/// deterministic amount the dedicated server validates. The menu consequence reads
/// <see cref="PlayerCaptivity.CurrentRansomAmount"/>, so the displayed and submitted prices remain
/// identical without trusting a client-selected gold amount.
/// </summary>
[HarmonyPatch(typeof(PlayerCaptivityCampaignBehavior), "menu_captivity_end_propose_ransom_on_init")]
internal static class PlayerCaptivityRansomPatches
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (ModInformation.IsServer || Campaign.Current == null || Hero.MainHero == null)
            return;

        PartyBase captor = Hero.MainHero.PartyBelongedToAsPrisoner;
        if (captor == null)
            return;

        Campaign.Current.PlayerCaptivity.CurrentRansomAmount =
            PlayerCaptivityServerHandler.CalculateAuthoritativeRansom(
                Hero.MainHero,
                captor);
    }
}
