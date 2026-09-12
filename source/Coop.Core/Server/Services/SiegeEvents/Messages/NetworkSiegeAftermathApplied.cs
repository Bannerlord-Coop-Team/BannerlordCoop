using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// The aftermath the server actually applied for a captured settlement, so client menus narrate the
/// right choice.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSiegeAftermathApplied : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int AftermathType { get; }

    public NetworkSiegeAftermathApplied(string settlementId, int aftermathType)
    {
        SettlementId = settlementId;
        AftermathType = aftermathType;
    }
}
