using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

internal sealed class ExtortionAlternativeSelection
{
    private readonly IMessageBroker broker;
    private readonly IControllerIdProvider controller;
    private Issue issue;
    private TroopRoster troops;
    private PartyScreenLogic screen;

    public bool IsSelecting => issue != null;
    public TroopRoster SelectedTroops => troops;
    public bool OwnsScreen(PartyScreenLogic logic) => ReferenceEquals(screen, logic);

    public ExtortionAlternativeSelection(IMessageBroker broker, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.controller = controller;
    }

    public void Begin(Issue selectedIssue, Hero companion)
    {
        Clear();
        if (selectedIssue == null || !selectedIssue.IsOngoingWithoutQuest || companion == null) return;
        issue = selectedIssue;
        troops = TroopRoster.CreateDummyTroopRoster();
        troops.AddToCounts(companion.CharacterObject, 1);
    }

    public bool Matches(IssueBase current) => issue != null && ReferenceEquals(issue, current) && issue.IsOngoingWithoutQuest;

    public bool CanAccept(IssueBase current) => Matches(current) && troops.TotalHeroes == 1 &&
        AlternativeSolutionStartRunner.DoTroopsSatisfyAlternativeSolution(issue, troops, out _);

    public void Open(IssueBase current)
    {
        if (!Matches(current)) return;
        var count = issue.GetTotalAlternativeSolutionNeededMenCount();
        if (count <= 1)
        {
            Campaign.Current.ConversationManager.ContinueConversation();
            return;
        }

        PartyScreenHelper.OpenScreenAsQuest(troops, new TextObject("{=FbLOFO88}Select troops for mission"),
            count + 1, issue.GetTotalAlternativeSolutionDurationInDays(),
            (left, leftPrisoners, right, rightPrisoners, leftLimit, rightLimit) =>
            {
                TextObject explanation = null;
                var valid = Matches(IssuesCampaignBehavior.GetIssueOwnersIssue()) && left.TotalHeroes == 1 &&
                    AlternativeSolutionStartRunner.DoTroopsSatisfyAlternativeSolution(issue, left, out explanation);
                return Tuple.Create(valid, explanation);
            },
            (leftParty, left, leftPrisoners, rightParty, right, rightPrisoners, cancelled) =>
            {
                if (cancelled) troops = Copy(left);
                screen = null;
                Campaign.Current.ConversationManager.ContinueConversation();
            }, IssuesCampaignBehavior.TroopTransferableDelegate);
    }

    public void PrepareScreen(PartyScreenLogic logic, ref PartyScreenLogicInitializationData data)
    {
        if (troops == null || !ReferenceEquals(data.LeftMemberRoster, troops)) return;
        screen = logic;
        data.LeftMemberRoster = TroopRoster.CreateDummyTroopRoster();
        data.RightMemberRoster = Copy(data.RightMemberRoster);
        data.RightPrisonerRoster = Copy(data.RightPrisonerRoster);
    }

    public void HideSelectedCompanion(PartyScreenLogic logic)
    {
        if (!ReferenceEquals(screen, logic)) return;
        // Select after the baseline is captured so a reset cannot duplicate the companion.
        foreach (var element in troops.GetTroopRoster())
        {
            logic.MemberRosters[1].AddToCounts(element.Character, -element.Number);
            logic.MemberRosters[0].AddToCounts(element.Character, element.Number);
        }
    }

    public TroopRoster GetCommitRoster(PartyScreenLogic logic)
    {
        if (!ReferenceEquals(screen, logic)) return logic.MemberRosters[1];
        troops = Copy(logic.MemberRosters[0]);
        var combined = Copy(logic.MemberRosters[1]);
        combined.Add(troops);
        return combined;
    }

    private static TroopRoster Copy(TroopRoster source)
    {
        var copy = TroopRoster.CreateDummyTroopRoster();
        copy.Add(source);
        return copy;
    }

    public void Accept(IssueBase current)
    {
        try
        {
            if (CanAccept(current))
                broker.Publish(this, new QuestTypeAlternativeAcceptTriggered(issue.IssueOwner, controller.ControllerId, troops));
        }
        finally
        {
            Clear();
        }
    }

    public void Clear()
    {
        issue = null;
        troops = null;
        screen = null;
    }
}
