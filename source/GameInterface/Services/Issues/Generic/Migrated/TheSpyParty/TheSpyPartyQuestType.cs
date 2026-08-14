namespace GameInterface.Services.Issues.Generic.Migrated.TheSpyParty;

using Issue = SandBox.Issues.TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue;
using Quest = SandBox.Issues.TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest;

[QuestTypeModule]
internal static class TheSpyPartyQuestType
{
    static TheSpyPartyQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("TheSpyParty")
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
