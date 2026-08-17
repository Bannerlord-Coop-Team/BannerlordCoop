using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

public static class QuestTypeRegistry
{
    private static readonly Dictionary<Type, QuestTypeDescriptor> ByIssueType = new();

    public static void Register(QuestTypeDescriptor descriptor)
    {
        if (descriptor?.IssueType == null) return;
        ByIssueType[descriptor.IssueType] = descriptor;
    }

    public static QuestTypeDescriptor Get(Type issueType) =>
        issueType != null && ByIssueType.TryGetValue(issueType, out var descriptor) ? descriptor : null;

    public static QuestTypeDescriptor Get(IssueBase issue) => issue != null ? Get(issue.GetType()) : null;

    public static bool IsRegistered(Type issueType) => issueType != null && ByIssueType.ContainsKey(issueType);

    internal static void ClearAllForTests() => ByIssueType.Clear();
}
