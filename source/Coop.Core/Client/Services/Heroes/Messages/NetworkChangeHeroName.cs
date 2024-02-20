using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the name of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeHeroName : ICommand
{
    [ProtoMember(1)]
    public HeroChangeNameData Data { get; }

    public NetworkChangeHeroName(HeroChangeNameData setNameData)
    {
        Data = setNameData;
    }
}
