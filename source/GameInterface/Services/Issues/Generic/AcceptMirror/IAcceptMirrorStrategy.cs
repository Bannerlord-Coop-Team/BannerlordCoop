using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

public interface IAcceptMirrorStrategy<in TQuest, in TFields> where TQuest : QuestBase
{
    void ForceWrite(TQuest quest, TFields fields);
}

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

public interface IAcceptFieldsReconstructor<in TQuest, in TItem> where TQuest : QuestBase
{
    void Reconstruct(TQuest quest, IReadOnlyList<TItem> items);
}

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
