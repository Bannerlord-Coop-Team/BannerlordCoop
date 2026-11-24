using Common.Messaging;
using ProtoBuf;

namespace Coop.Naval.PortScreen.Messages;

/// <summary>
/// [Server, sent to the owner's client] One of the player's ships was moored at or taken back from a port.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShipMooringChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string ShipId;

    [ProtoMember(3)]
    public readonly bool IsMoored;

    public NetworkShipMooringChanged(string settlementId, string shipId, bool isMoored)
    {
        SettlementId = settlementId;
        ShipId = shipId;
        IsMoored = isMoored;
    }
}
