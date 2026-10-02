using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct ArtisanTraitProgress
{
    [ProtoMember(1)] public readonly string TraitId;
    [ProtoMember(2)] public readonly int Xp;
    [ProtoMember(3)] public readonly int Level;

    public ArtisanTraitProgress(string traitId, int xp, int level)
    {
        TraitId = traitId;
        Xp = xp;
        Level = level;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanTraitProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly ArtisanTraitProgress[] Traits;

    public NetworkArtisanTraitProgress(string heroId, ArtisanTraitProgress[] traits)
    {
        HeroId = heroId;
        Traits = traits;
    }
}
