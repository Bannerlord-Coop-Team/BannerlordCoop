using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the _name of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkNameChanged : ICommand
{
    [ProtoMember(1)]
    public string NewName { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkNameChanged(string newName, string heroId)
    {
        NewName = newName;
        HeroId = heroId;
    }
}
