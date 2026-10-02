using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Migrated.ScoutEnemyGarrisons;

using Issue = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

[QuestTypeModule]
internal static class ScoutEnemyGarrisonsQuestType
{
    static ScoutEnemyGarrisonsQuestType()
    {
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("ScoutEnemyGarrisons")
            .WithQuestSolutionAccept(new AcceptMirror())
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue, new ScoutEnemyGarrisonsIssueChanged(issue, true)))
            .Build());
    }

    private sealed class AcceptMirror : IRaceArbitratedAcceptMirrorStrategy<ScoutEnemyGarrisonsAccept>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service)) service.Accept(owner);
        }

        public bool TryCaptureQuestFields(Hero owner, out ScoutEnemyGarrisonsAccept fields)
        {
            fields = default;
            return ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service) && service.CaptureAcceptance(owner, out fields);
        }

        public void MirrorQuestAccepted(Hero owner, ScoutEnemyGarrisonsAccept fields)
        {
            if (ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service)) service.MirrorAcceptance(owner, fields);
        }

        public void RejectAcceptance(Hero owner)
        {
            // Clients never start an optimistic quest; a rejection must preserve the winner's mirror.
            if (ModInformation.IsServer && owner.Issue?.IssueQuest is Quest quest && quest.IsOngoing)
                quest.CompleteQuestWithCancel();
        }
    }
}
