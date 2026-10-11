using Common.Messaging;
using ProtoBuf;

namespace Coop.Naval.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyShipsGoldRecovered : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string PartyOwnerId;

    [ProtoMember(3)]
    public readonly int Gold;

    public NetworkNotifyShipsGoldRecovered(
        string clanId,
        string partyOwnerId,
        int gold)
    {
        ClanId = clanId;
        PartyOwnerId = partyOwnerId;
        Gold = gold;
    }
}
