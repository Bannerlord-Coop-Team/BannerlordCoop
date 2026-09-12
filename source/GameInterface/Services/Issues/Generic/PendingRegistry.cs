using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

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
