using System;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.Gates;

public sealed record GateEntry<TInstance>(
    string MethodName,
    GateKind Kind,
    Func<TInstance, Hero> QuestGiverSelector);

public sealed class GenericGates<TInstance>
{
    private readonly System.Collections.Generic.List<GateEntry<TInstance>> _entries = new();

    public System.Collections.Generic.IReadOnlyList<GateEntry<TInstance>> Entries => _entries;

    public GenericGates<TInstance> Add(GateEntry<TInstance> entry)
    {
        _entries.Add(entry);
        return this;
    }
}
