using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the IsPregnant of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPregnantChanged : ICommand
{
    [ProtoMember(1)]
    public bool IsPregnant { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkPregnantChanged(bool isPregnant, string heroId)
    {
        IsPregnant = isPregnant;
        HeroId = heroId;
    }
}
