using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the BeardTags of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBeardTagsChanged : ICommand
{
    [ProtoMember(1)]
    public string BeardTags { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkBeardTagsChanged(string beardTags, string heroId)
    {
        BeardTags = beardTags;
        HeroId = heroId;
    }
}
