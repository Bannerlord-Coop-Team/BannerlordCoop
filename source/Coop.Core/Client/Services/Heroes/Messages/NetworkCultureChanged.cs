using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the Culture of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCultureChanged : ICommand
{
    [ProtoMember(1)]
    public string CultureStringId { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkCultureChanged(string cultureStringId, string heroId)
    {
        CultureStringId = cultureStringId;
        HeroId = heroId;
    }
}