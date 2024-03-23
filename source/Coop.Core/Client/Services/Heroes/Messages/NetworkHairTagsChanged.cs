using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the HairTags of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkHairTagsChanged : ICommand
{
    [ProtoMember(1)]
    public string HairTags { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkHairTagsChanged(string hairTags, string heroId)
    {
        HairTags = hairTags;
        HeroId = heroId;
    }
}
