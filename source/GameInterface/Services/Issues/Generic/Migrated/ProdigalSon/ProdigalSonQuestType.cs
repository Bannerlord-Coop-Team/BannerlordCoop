namespace GameInterface.Services.Issues.Generic.Migrated.ProdigalSon;

using Issue = SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssue;
using Quest = SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssueQuest;

[QuestTypeModule]
internal static class ProdigalSonQuestType
{
    static ProdigalSonQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("ProdigalSon")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
