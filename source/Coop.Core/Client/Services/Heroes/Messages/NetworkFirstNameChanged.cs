using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the first name of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkFirstNameChanged : ICommand
{
    [ProtoMember(1)]
    public string NewName { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkFirstNameChanged(string newName, string heroId)
    {
        NewName = newName;
        HeroId = heroId;
    }
}
