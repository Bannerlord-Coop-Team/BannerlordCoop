using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the _defaultAge of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkDefaultAgeChanged : ICommand
{
    [ProtoMember(1)]
    public float Age { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkDefaultAgeChanged(float age, string heroId)
    {
        Age = age;
        HeroId = heroId;
    }
}
