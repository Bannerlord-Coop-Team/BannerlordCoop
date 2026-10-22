using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>Keeps selection attribution at the issue conversation that opens the quest picker.</summary>
[HarmonyPatch]
internal class AlternativeSolutionPickerPatches
{
    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyPrefix]
    private static void CompanionPrefix()
    {
        OpenPrefix();
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_4_consequence))]
    [HarmonyPrefix]
    private static void OpenPrefix()
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IAlternativeSolutionPicker>(out var picker))
            picker.Open(Hero.OneToOneConversationHero?.Issue);
    }

    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.Initialize))]
    [HarmonyPostfix]
    private static void InitializePostfix(PartyScreenLogic __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IAlternativeSolutionPicker>(out var picker))
            picker.Track(__instance);
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    [HarmonyPrefix]
    private static bool CancelPrefix()
    {
        var issue = Hero.OneToOneConversationHero?.Issue;
        if (ModInformation.IsClient && issue != null && ContainerProvider.TryResolve<IAlternativeSolutionPicker>(out var picker))
        {
            picker.Close(issue, cancel: true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void FinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IAlternativeSolutionPicker>(out var picker))
            picker.Close(__instance, cancel: true);
    }
}
