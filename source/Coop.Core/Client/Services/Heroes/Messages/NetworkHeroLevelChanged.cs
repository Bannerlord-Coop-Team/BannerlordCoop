using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the Level of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkHeroLevelChanged : ICommand
{
    [ProtoMember(1)]
    public int HeroLevel { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkHeroLevelChanged(int heroLevel, string heroId)
    {
        HeroLevel = heroLevel;
        HeroId = heroId;
    }
}
