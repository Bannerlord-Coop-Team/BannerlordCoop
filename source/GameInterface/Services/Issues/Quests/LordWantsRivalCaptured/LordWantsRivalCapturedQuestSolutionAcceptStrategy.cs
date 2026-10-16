using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;

internal sealed class LordWantsRivalCapturedQuestSolutionAcceptStrategy :
    IQuestSolutionAcceptStrategy, IQuestSolutionAcceptRejectionStrategy
{
    private readonly ILordWantsRivalCapturedQuestService service;

    public LordWantsRivalCapturedQuestSolutionAcceptStrategy(ILordWantsRivalCapturedQuestService service)
    {
        this.service = service;
    }

    public byte[] Capture(IssueBase issue)
    {
        // The conversation hook prepares the baseline before vanilla creates the quest. Once the
        // quest exists, capturing its acceptance must not send another baseline request.
        if (issue?.IssueQuest == null)
            service.PrepareAcceptance(issue?.IssueOwner);
        if (issue?.IssueQuest is Quest quest && quest.IsOngoing &&
            !Campaign.Current.QuestManager.Quests.Contains(quest))
        {
            using (new QuestSolutionStartAuthorityGuard())
                quest.QuestAcceptedConsequences();
        }

        return service.TryCaptureFrameworkQuestFields(issue?.IssueOwner, out var fields)
            ? GenericAcceptFieldsSerializer.Serialize(fields)
            : System.Array.Empty<byte>();
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (issue is not Issue || captured == null || captured.Length == 0) return;
        var fields = GenericAcceptFieldsSerializer.Deserialize<RivalCapturedAcceptFields>(captured);
        service.ApplyFrameworkQuestAcceptance((Issue)issue, fields);
    }

    public void Reject(IssueBase issue) => service.RejectAcceptance(issue?.IssueOwner);
}
