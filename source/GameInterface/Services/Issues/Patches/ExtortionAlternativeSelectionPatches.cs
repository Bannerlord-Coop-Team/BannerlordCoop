using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class ExtortionAlternativeSelectionPatches
{
    private static bool TryGet(out ExtortionAlternativeSelection selection, out IssueBase issue)
    {
        selection = null;
        issue = null;
        if (ModInformation.IsServer) return false;
        issue = IssuesCampaignBehavior.GetIssueOwnersIssue();
        return ContainerProvider.TryResolve(out selection) && (issue is Issue || selection.IsSelecting);
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    [HarmonyPrefix]
    private static bool SelectCompanion()
    {
        if (!TryGet(out var selection, out var issue)) return true;
        selection.Begin(issue as Issue, ConversationSentence.SelectedRepeatObject as Hero);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_4_consequence))]
    [HarmonyPrefix]
    private static bool OpenScreen()
    {
        if (!TryGet(out var selection, out var issue)) return true;
        selection.Open(issue);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_condition))]
    [HarmonyPrefix]
    private static bool CanAccept(ref bool __result)
    {
        if (!TryGet(out var selection, out var issue)) return true;
        if (issue != null)
            MBTextManager.SetTextVariable("IssueAlternativeSolutionResponseByIssueGiverText", issue.IssueAlternativeSolutionResponseByIssueGiver);
        __result = selection.CanAccept(issue);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence))]
    [HarmonyPrefix]
    private static bool Accept()
    {
        if (!TryGet(out var selection, out var issue)) return true;
        selection.Accept(issue);
        return false;
    }

    [HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    [HarmonyPrefix]
    private static bool Cancel()
    {
        if (!TryGet(out var selection, out _)) return true;
        selection.Clear();
        return false;
    }
}

[HarmonyPatch(typeof(PartyScreenLogic))]
internal class ExtortionAlternativeScreenPatch
{
    [HarmonyPatch(nameof(PartyScreenLogic.Initialize))]
    [HarmonyPrefix]
    private static void Prefix(PartyScreenLogic __instance, ref PartyScreenLogicInitializationData initializationData)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ExtortionAlternativeSelection>(out var selection))
            selection.PrepareScreen(__instance, ref initializationData);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.Initialize))]
    [HarmonyPostfix]
    private static void Postfix(PartyScreenLogic __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ExtortionAlternativeSelection>(out var selection))
            selection.HideSelectedCompanion(__instance);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.TransferTroop))]
    [HarmonyPrefix]
    private static void TransferPrefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, out int? __state)
    {
        __state = null;
        if (ModInformation.IsClient && command.Type == PartyScreenLogic.TroopType.Member &&
            command.RosterSide == PartyScreenLogic.PartyRosterSide.Right &&
            ContainerProvider.TryResolve<ExtortionAlternativeSelection>(out var selection) && selection.OwnsScreen(__instance))
            __state = __instance.MemberRosters[0].GetElementXp(command.Character);
    }

    [HarmonyPatch(nameof(PartyScreenLogic.TransferTroop))]
    [HarmonyPostfix]
    private static void TransferPostfix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, int? __state)
    {
        if (!__state.HasValue) return;
        // Vanilla credits the left stack without debiting the private right stack.
        var transferredXp = __instance.MemberRosters[0].GetElementXp(command.Character) - __state.Value;
        __instance.MemberRosters[1].AddXpToTroop(command.Character, -transferredXp);
        __instance.MemberRosters[1].UpdateVersion();
    }
}

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.EndConversation))]
internal class ExtortionAlternativeConversationEndPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ExtortionAlternativeSelection>(out var selection))
            selection.Clear();
    }
}
