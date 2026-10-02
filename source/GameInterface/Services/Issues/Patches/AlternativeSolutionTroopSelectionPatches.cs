using Common;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
internal class AlternativeSolutionTroopSelectionPatches
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        var owner = Hero.OneToOneConversationHero;
        if (ModInformation.IsServer || QuestTypeRegistry.Get(owner?.Issue)?.SupportsAlternativeAccept != true) return true;

        if (!ContainerProvider.TryResolve<IAlternativeSolutionTroopSelection>(out var troopSelection)) return true;
        troopSelection.Rollback(owner, closeScreen: false);
        return false;
    }
}
