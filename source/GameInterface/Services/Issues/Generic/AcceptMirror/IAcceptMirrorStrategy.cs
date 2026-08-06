using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

/// <summary>
/// Force-write primitive underlying <see cref="IRaceArbitratedAcceptMirrorStrategy{TFields}"/>'s own
/// "TFields gets force-written onto that Quest object by reflection" step. Names HOW a captured accept-time
/// field shape gets written back onto a live <typeparamref name="TQuest"/> instance; deliberately separate from
/// <see cref="IRaceArbitratedAcceptMirrorStrategy{TFields}"/> itself (which also owns the replay/capture/reject
/// protocol, not just the write step), so a concrete strategy can compose one of these instead of hand-rolling
/// its own <see cref="FieldInfo.SetValue(object,object)"/> calls.
///
/// Four concrete shapes:
/// <list type="bullet">
/// <item><see cref="GenericAcceptMirror{TQuest,TFields}"/> - N named fields off an arbitrary TFields shape.</item>
/// <item><see cref="ScalarFieldAcceptMirror{TQuest,TValue}"/> - exactly one scalar field.</item>
/// <item><see cref="IndexAcceptMirror{TQuest}"/> - a single <c>int</c> index field.</item>
/// <item><see cref="ListAcceptMirror{TQuest,TItem}"/> - list-reconstruction escape hatch, via
/// <see cref="IAcceptFieldsReconstructor{TQuest,TItem}"/>.</item>
/// </list>
/// </summary>
public interface IAcceptMirrorStrategy<in TQuest, in TFields> where TQuest : QuestBase
{
    void ForceWrite(TQuest quest, TFields fields);
}

/// <summary>Reflection-based force-write for an arbitrary named-field shape - the dominant real case (multiple
/// named fields written together). Each writer pairs a reflected <see cref="FieldInfo"/> with a selector pulling
/// the matching value out of <typeparamref name="TFields"/>.</summary>
public sealed class GenericAcceptMirror<TQuest, TFields> : IAcceptMirrorStrategy<TQuest, TFields> where TQuest : QuestBase
{
    private readonly IReadOnlyList<(FieldInfo Field, Func<TFields, object> Selector)> _writers;

    public GenericAcceptMirror(params (FieldInfo Field, Func<TFields, object> Selector)[] writers)
    {
        _writers = writers ?? Array.Empty<(FieldInfo, Func<TFields, object>)>();
    }

    public void ForceWrite(TQuest quest, TFields fields)
    {
        if (quest == null) return;
        foreach (var (field, selector) in _writers)
        {
            field.SetValue(quest, selector(fields));
        }
    }
}

/// <summary>Force-write for exactly one scalar field - the simplest real shape (a single captured value with no
/// sibling fields to write alongside it).</summary>
public sealed class ScalarFieldAcceptMirror<TQuest, TValue> : IAcceptMirrorStrategy<TQuest, TValue> where TQuest : QuestBase
{
    private readonly FieldInfo _field;

    public ScalarFieldAcceptMirror(FieldInfo field)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
    }

    public void ForceWrite(TQuest quest, TValue value)
    {
        if (quest == null) return;
        _field.SetValue(quest, value);
    }
}

/// <summary>Force-write for an index-shaped accept payload (a plain <c>int</c> field is just a
/// <c>TFields</c> instantiation like any other). Kept as its own named type - rather than requiring every caller
/// to reach for <see cref="ScalarFieldAcceptMirror{TQuest,TValue}"/> with <c>TValue = int</c> - purely for
/// call-site clarity at index-shaped instances.</summary>
public sealed class IndexAcceptMirror<TQuest> : IAcceptMirrorStrategy<TQuest, int> where TQuest : QuestBase
{
    private readonly FieldInfo _indexField;

    public IndexAcceptMirror(FieldInfo indexField)
    {
        _indexField = indexField ?? throw new ArgumentNullException(nameof(indexField));
    }

    public void ForceWrite(TQuest quest, int index)
    {
        if (quest == null) return;
        _indexField.SetValue(quest, index);
    }
}

/// <summary>Escape hatch for a list-shaped captured field, which generally can't be force-written by a single
/// <see cref="FieldInfo.SetValue(object,object)"/> call the way a scalar can - the live Quest instance typically
/// owns an engine-managed collection (e.g. an <c>MBReadOnlyList</c>/<c>MBList</c>-backed field) that needs its
/// own reconstruction logic instead of a bare reference swap.</summary>
public interface IAcceptFieldsReconstructor<in TQuest, in TItem> where TQuest : QuestBase
{
    void Reconstruct(TQuest quest, IReadOnlyList<TItem> items);
}

/// <summary>Force-write for a list-shaped captured field, delegating the actual reconstruction to a
/// type-specific <see cref="IAcceptFieldsReconstructor{TQuest,TItem}"/> (plain reflection can't safely swap an
/// engine-managed collection reference the way it can a scalar field).</summary>
public sealed class ListAcceptMirror<TQuest, TItem> : IAcceptMirrorStrategy<TQuest, IReadOnlyList<TItem>> where TQuest : QuestBase
{
    private readonly IAcceptFieldsReconstructor<TQuest, TItem> _reconstructor;

    public ListAcceptMirror(IAcceptFieldsReconstructor<TQuest, TItem> reconstructor)
    {
        _reconstructor = reconstructor ?? throw new ArgumentNullException(nameof(reconstructor));
    }

    public void ForceWrite(TQuest quest, IReadOnlyList<TItem> items)
    {
        if (quest == null) return;
        _reconstructor.Reconstruct(quest, items);
    }
}
