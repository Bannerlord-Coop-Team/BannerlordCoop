using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

public interface IIssueGenerationRegistry
{
    int Bump(Hero owner);
    void SetGeneration(Hero owner, int generation);
    bool TryGetGeneration(Hero owner, out int generation);
    IReadOnlyCollection<KeyValuePair<Hero, int>> Snapshot();
    void RestoreAll(IEnumerable<KeyValuePair<Hero, int>> entries);
}

internal sealed class IssueGenerationRegistry : IIssueGenerationRegistry
{
    private readonly PendingRegistry<int> registry = new();

    public int Bump(Hero owner)
    {
        registry.TryGet(owner, out var current);
        var next = current + 1;
        registry.Set(owner, next);
        return next;
    }

    public void SetGeneration(Hero owner, int generation) => registry.Set(owner, generation);

    public bool TryGetGeneration(Hero owner, out int generation) => registry.TryGet(owner, out generation);

    public IReadOnlyCollection<KeyValuePair<Hero, int>> Snapshot() => registry.Snapshot();

    public void RestoreAll(IEnumerable<KeyValuePair<Hero, int>> entries) => registry.RestoreAll(entries);
}
