using Issue = SandBox.Issues.RivalGangMovingInIssueBehavior.RivalGangMovingInIssue;
using Quest = SandBox.Issues.RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest;
using GameInterface.Services.Issues.Patches;

namespace GameInterface.Services.Issues.Generic.Migrated.RivalGangMovingIn;

[QuestTypeModule]
internal static class RivalGangMovingInQuestType
{
    static RivalGangMovingInQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("RivalGangMovingIn")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(SandBox.Issues.RivalGangMovingInIssueBehavior));
    }
}
