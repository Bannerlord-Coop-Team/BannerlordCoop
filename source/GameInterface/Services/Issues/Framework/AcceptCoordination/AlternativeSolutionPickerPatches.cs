using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>Keeps selection attribution at the issue conversation that opens the quest picker.</summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class AlternativeSolutionPickerPatches
{
    private sealed class SelectionState : System.IDisposable
    {
        private readonly AllowedThread allowedThread;

        public SelectionState() => allowedThread = new AllowedThread();

        public void Dispose() => allowedThread.Dispose();
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyPrefix]
    private static void CompanionPrefix(out SelectionState __state)
    {
        __state = null;
        if (ModInformation.IsClient &&
            Hero.OneToOneConversationHero?.Issue != null &&
            ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry) &&
            registry.TryGet(Hero.OneToOneConversationHero.Issue.GetType(), out var descriptor) &&
            descriptor.AlternativeSolutionAcceptStrategy != null)
        {
            __state = new SelectionState();
        }

        OpenPrefix();
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyFinalizer]
    private static void CompanionFinalizer(SelectionState __state) => __state?.Dispose();

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
            // Native ClosePartyPresentation pops the picker after this conversation callback returns.
            picker.Close(issue, cancel: true, closeScreen: false);
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
