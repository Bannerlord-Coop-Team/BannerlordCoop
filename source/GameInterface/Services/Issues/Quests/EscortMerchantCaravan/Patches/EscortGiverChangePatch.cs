using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.ChangeIssueOwner))]
internal class EscortGiverChangePatch
{
    private static bool Prefix(IssueBase issue, Hero newOwner, out Hero __state)
    {
        __state = null;
        if (!(issue is Issue)) return true;
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();
        var previous = issue.IssueOwner;
        if (issue.IssueQuest is Quest quest)
        {
            // Moving the issue first would hide the dead giver's quest from IssueManager.OnHeroKilled.
            if (previous.IsDead && quest.IsOngoing && ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            {
                var log = new TextObject("{=rTvWdMXF}{DIED_HERO.LINK} died and your agreement with {?DIED_HERO.GENDER}her{?}him{\\?} canceled.");
                StringHelpers.SetCharacterProperties("DIED_HERO", previous.CharacterObject, log);
                dispatcher.RunAsOwners<Quest>(current =>
                {
                    if (current == quest) current.CompleteQuestWithCancel(log);
                });
            }
            return false;
        }
        if (newOwner == null || newOwner == previous || newOwner.Issue != null) return false;
        __state = previous;
        return true;
    }

    private static void Postfix(IssueBase issue, Hero __state)
    {
        if (ModInformation.IsServer && __state != null && issue is Issue escort && issue.IssueOwner != __state)
            MessageBroker.Instance.Publish(issue, new EscortGiverChanged(escort, __state));
    }
}
