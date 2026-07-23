using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the SpcDaysInLocation of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSpcDaysInLocationChanged : ICommand
{
    [ProtoMember(1)]
    public int Days { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkSpcDaysInLocationChanged(int days, string heroId)
    {
        Days = days;
        HeroId = heroId;
    }
}
