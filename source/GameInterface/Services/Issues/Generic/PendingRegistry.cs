using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Generalizes the Hero-keyed pending-value registry shape already hand-implemented by
/// <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/> (<c>Dictionary&lt;Hero, string&gt;</c>), for reuse
/// by future quest-type-specific registries with the same shape. Not yet consumed anywhere - rewiring an
/// existing registry to delegate to this primitive is a separate, purely-mechanical follow-up with its own
/// regression surface across every quest type that already depends on it.
///
/// Hero-keyed for the same reason the existing registry is: each peer holds a different object
/// instance for "the same logical issue," converging only on the shared owner-<see cref="Hero"/> key. <c>Set</c>
/// writes only from an authoritative broadcast, <c>TryGet</c> is a non-destructive read, <c>Clear(Hero)</c>
/// drains a single entry on genuine finalize, <c>ClearAll()</c> wipes everything before a load-time repopulate,
/// <c>Snapshot()</c> feeds <c>SyncData</c> persistence, <c>RestoreAll</c> is the load-time repopulate itself.
/// </summary>
internal sealed class PendingRegistry<TValue>
{
    private readonly Dictionary<Hero, TValue> _byOwner = new();

    public void Set(Hero owner, TValue value)
    {
        if (owner != null) _byOwner[owner] = value;
    }

    public bool TryGet(Hero owner, out TValue value)
    {
        value = default;
        return owner != null && _byOwner.TryGetValue(owner, out value);
    }

    public void Clear(Hero owner)
    {
        if (owner != null) _byOwner.Remove(owner);
    }

    public void ClearAll() => _byOwner.Clear();

    public IReadOnlyCollection<KeyValuePair<Hero, TValue>> Snapshot() => _byOwner.ToArray();

    public void RestoreAll(IEnumerable<KeyValuePair<Hero, TValue>> entries)
    {
        ClearAll();
        if (entries == null) return;

        foreach (var kvp in entries)
        {
            if (kvp.Key != null) _byOwner[kvp.Key] = kvp.Value;
        }
    }
}
