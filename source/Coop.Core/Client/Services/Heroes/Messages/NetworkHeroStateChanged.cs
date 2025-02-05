using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the _heroState of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkHeroStateChanged : ICommand
{
    [ProtoMember(1)]
    public int HeroState { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkHeroStateChanged(int heroState, string heroId)
    {
        HeroState = heroState;
        HeroId = heroId;
    }
}
