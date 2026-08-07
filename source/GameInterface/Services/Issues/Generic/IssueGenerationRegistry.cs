using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

internal static class IssueGenerationRegistry
{
    private static readonly PendingRegistry<int> Registry = new();

    public static int Bump(Hero owner)
    {
        Registry.TryGet(owner, out var current);
        var next = current + 1;
        Registry.Set(owner, next);
        return next;
    }

    public static void SetGeneration(Hero owner, int generation) => Registry.Set(owner, generation);

    public static bool TryGetGeneration(Hero owner, out int generation) => Registry.TryGet(owner, out generation);

    public static IReadOnlyCollection<KeyValuePair<Hero, int>> Snapshot() => Registry.Snapshot();

    public static void RestoreAll(IEnumerable<KeyValuePair<Hero, int>> entries) => Registry.RestoreAll(entries);
}
