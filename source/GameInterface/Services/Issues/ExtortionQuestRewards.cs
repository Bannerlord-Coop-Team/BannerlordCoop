using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;
using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using QuestState = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;
using Result = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestResult;

internal interface IExtortionQuestRewards
{
    void Request(Quest quest, in Result result);
    byte CaptureChoice(Issue issue);
    bool Validate(Issue issue, MobileParty playerParty);
    void Apply(Quest quest);
}

internal sealed class ExtortionQuestRewards : IExtortionQuestRewards
{
    private readonly ConditionalWeakTable<Quest, Selection> choices = new();
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IControllerIdProvider controller;
    private readonly IMessageBroker broker;

    public ExtortionQuestRewards(IIssueOwnershipRegistry ownership, IControllerIdProvider controller, IMessageBroker broker)
    {
        this.ownership = ownership;
        this.controller = controller;
        this.broker = broker;
    }

    public void Request(Quest quest, in Result result)
    {
        if (ModInformation.IsServer || !quest.IsOngoing ||
            quest._currentState != QuestState.DesertersAreDefeated || !ownership.IsLocalPeerOwner(quest.QuestGiver)) return;
        byte choice = result.Equals(quest._questResultSuccess1) ? (byte)1 :
            result.Equals(quest._questResultSuccess2) ? (byte)2 :
            result.Equals(quest._questResultSuccess3) ? (byte)3 : (byte)0;
        if (choice == 0) return;
        choices.Remove(quest);
        choices.Add(quest, new Selection(choice));
        broker.Publish(quest, new QuestTerminalOutcomeTriggered(quest.QuestGiver, controller.ControllerId,
            IssueFinalizeReason.QuestSuccess));
    }

    public byte CaptureChoice(Issue issue) => issue.IssueQuest is Quest quest && choices.TryGetValue(quest, out var selection)
        ? selection.Choice : (byte)0;

    public bool Validate(Issue issue, MobileParty playerParty) => issue.IssueQuest is Quest quest && quest.IsOngoing &&
        quest._currentState == QuestState.DesertersAreDefeated && playerParty != null && quest.QuestSettlement != null &&
        playerParty.CurrentSettlement == quest.QuestSettlement &&
        QuestSuccessProofContext.Current >= 1 && QuestSuccessProofContext.Current <= 3;

    public void Apply(Quest quest)
    {
        switch (QuestSuccessProofContext.Current)
        {
            case 1: quest.ApplyQuestResult(quest._questResultSuccess1); break;
            case 2: quest.ApplyQuestResult(quest._questResultSuccess2); break;
            case 3: quest.ApplyQuestResult(quest._questResultSuccess3); break;
            default: return;
        }
        quest.AddLog(quest.OnQuestSucceededLogText);
        quest.CompleteQuestWithSuccess();
    }

    private sealed class Selection
    {
        public byte Choice { get; }

        public Selection(byte choice) => Choice = choice;
    }
}
