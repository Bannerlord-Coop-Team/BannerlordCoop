using GameInterface.Services.Issues.Patches;
﻿namespace GameInterface.Services.Issues.Generic.Migrated.RuralNotableInnAndOut;

using Issue = SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue;
using Quest = SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssueQuest;

[QuestTypeModule]
internal static class RuralNotableInnAndOutQuestType
{
    static RuralNotableInnAndOutQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("RuralNotableInnAndOut")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .Build();

        QuestTypeRegistry.Register(descriptor);
        DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior));
    }
}
