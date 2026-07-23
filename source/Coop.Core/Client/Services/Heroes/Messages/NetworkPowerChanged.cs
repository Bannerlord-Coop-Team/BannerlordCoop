using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the _power of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPowerChanged : ICommand
{
    [ProtoMember(1)]
    public float Power { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkPowerChanged(float power, string heroId)
    {
        Power = power;
        HeroId = heroId;
    }
}
