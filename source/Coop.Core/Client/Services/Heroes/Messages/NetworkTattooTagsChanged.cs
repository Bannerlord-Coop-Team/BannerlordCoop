using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the TattooTags of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkTattooTagsChanged : ICommand
{
    [ProtoMember(1)]
    public string TattooTags { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkTattooTagsChanged(string tattooTags, string heroId)
    {
        TattooTags = tattooTags;
        HeroId = heroId;
    }
}
