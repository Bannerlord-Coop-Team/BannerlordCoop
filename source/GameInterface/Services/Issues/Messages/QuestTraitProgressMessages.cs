using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Issues.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestQuestTraitProgress : ICommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly Dictionary<string, int> Values;

    public RequestQuestTraitProgress(string heroId, Dictionary<string, int> values)
    {
        HeroId = heroId;
        Values = values;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestQuestTraitXpChange : ICommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly string TraitId;
    [ProtoMember(3)] public readonly int Amount;
    [ProtoMember(4)] public readonly int Context;
    [ProtoMember(5)] public readonly string ReferenceHeroId;

    public RequestQuestTraitXpChange(string heroId, string traitId, int amount, int context, string referenceHeroId)
    {
        HeroId = heroId;
        TraitId = traitId;
        Amount = amount;
        Context = context;
        ReferenceHeroId = referenceHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkQuestTraitProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly Dictionary<string, int> Values;
    [ProtoMember(3)] public readonly string TraitId;
    [ProtoMember(4)] public readonly int OldLevel;
    [ProtoMember(5)] public readonly int Level;
    [ProtoMember(6)] public readonly int Amount;
    [ProtoMember(7)] public readonly int Context;
    [ProtoMember(8)] public readonly string ReferenceHeroId;

    public NetworkQuestTraitProgress(string heroId, Dictionary<string, int> values, string traitId = null,
        int oldLevel = 0, int level = 0, int amount = 0, int context = 0, string referenceHeroId = null)
    {
        HeroId = heroId;
        Values = values;
        TraitId = traitId;
        OldLevel = oldLevel;
        Level = level;
        Amount = amount;
        Context = context;
        ReferenceHeroId = referenceHeroId;
    }
}
