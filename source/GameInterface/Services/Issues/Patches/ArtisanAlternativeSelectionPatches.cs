using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class ArtisanAlternativeSelectionPatches
{
    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyPrefix]
    private static void Begin()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (Hero.OneToOneConversationHero?.Issue is Issue issue &&
            ContainerProvider.TryResolve<IArtisanAlternativeSelection>(out var selection))
            selection.Begin(issue, MobileParty.MainParty.MemberRoster);
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    [HarmonyPrefix]
    private static bool Cancel()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        var giver = Hero.OneToOneConversationHero;
        if (giver?.Issue is not Issue) return true;
        if (ContainerProvider.TryResolve<IArtisanAlternativeSelection>(out var selection)) selection.Finish(giver);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence))]
    [HarmonyPrefix]
    private static bool Accept()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        var giver = Hero.OneToOneConversationHero;
        if (giver?.Issue is not Issue issue || issue.IsOngoingWithoutQuest) return true;
        if (ContainerProvider.TryResolve<IArtisanAlternativeSelection>(out var selection)) selection.Finish(giver);
        return false;
    }
}
