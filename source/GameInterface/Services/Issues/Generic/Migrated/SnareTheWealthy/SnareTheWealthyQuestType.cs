using Issue = SandBox.Issues.SnareTheWealthyIssueBehavior.SnareTheWealthyIssue;
using Quest = SandBox.Issues.SnareTheWealthyIssueBehavior.SnareTheWealthyIssueQuest;

namespace GameInterface.Services.Issues.Generic.Migrated.SnareTheWealthy;

[QuestTypeModule]
internal static class SnareTheWealthyQuestType
{
    static SnareTheWealthyQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("SnareTheWealthy")
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
