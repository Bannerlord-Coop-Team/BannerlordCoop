using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the _birthDay of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBirthDayChanged : ICommand
{
    [ProtoMember(1)]
    public long BirthDay { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkBirthDayChanged(long birthDay, string heroId)
    {
        BirthDay = birthDay;
        HeroId = heroId;
    }
}
