using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to change the _homeSettlement of a hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkHomeSettlementChanged : ICommand
{
    [ProtoMember(1)]
    public string SettlementStringId { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkHomeSettlementChanged(string settlementStringId, string heroId)
    {
        SettlementStringId = settlementStringId;
        HeroId = heroId;
    }
}
