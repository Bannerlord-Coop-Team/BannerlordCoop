using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Registry of migrated quest types, keyed by issue type. A migrated type registers its
/// <see cref="QuestTypeDescriptor"/> here; unmigrated types are never registered, so <see cref="Get(Type)"/>
/// returns null for them and the Generic/ infrastructure is never consulted for them - they keep running on
/// their existing hand-written Interfaces/Messages/Handlers/Patches files.
///
/// Process-wide static state (same pattern as <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>):
/// registration happens once, at mod bootstrap, for the lifetime of the process.
/// <see cref="ClearAllForTests"/> resets it between unit tests.
/// </summary>
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

    /// <summary>Test-only reset hook, mirroring <c>VillageNeedsToolsIssueOwnership.ClearAll</c>'s own test
    /// usage.</summary>
    internal static void ClearAllForTests() => ByIssueType.Clear();
}
