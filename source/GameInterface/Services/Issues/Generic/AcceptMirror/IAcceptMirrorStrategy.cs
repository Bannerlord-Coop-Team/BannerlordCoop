using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

public interface IAcceptMirrorStrategy<in TQuest, in TFields> where TQuest : QuestBase
{
    void ForceWrite(TQuest quest, TFields fields);
}

public sealed class GenericAcceptMirror<TQuest, TFields> : IAcceptMirrorStrategy<TQuest, TFields> where TQuest : QuestBase
{
    private readonly IReadOnlyList<Action<TQuest, TFields>> _writers;

    public GenericAcceptMirror(params Action<TQuest, TFields>[] writers)
    {
        _writers = writers ?? Array.Empty<Action<TQuest, TFields>>();
    }

    public void ForceWrite(TQuest quest, TFields fields)
    {
        if (quest == null) return;
        foreach (var writer in _writers)
        {
            writer(quest, fields);
        }
    }
}

public sealed class ScalarFieldAcceptMirror<TQuest, TValue> : IAcceptMirrorStrategy<TQuest, TValue> where TQuest : QuestBase
{
    private readonly Action<TQuest, TValue> _writer;

    public ScalarFieldAcceptMirror(Action<TQuest, TValue> writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void ForceWrite(TQuest quest, TValue value)
    {
        if (quest == null) return;
        _writer(quest, value);
    }
}

public sealed class IndexAcceptMirror<TQuest> : IAcceptMirrorStrategy<TQuest, int> where TQuest : QuestBase
{
    private readonly Action<TQuest, int> _indexWriter;

    public IndexAcceptMirror(Action<TQuest, int> indexWriter)
    {
        _indexWriter = indexWriter ?? throw new ArgumentNullException(nameof(indexWriter));
    }

    public void ForceWrite(TQuest quest, int index)
    {
        if (quest == null) return;
        _indexWriter(quest, index);
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
