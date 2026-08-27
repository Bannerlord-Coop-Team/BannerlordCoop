using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestLocationRosterSnapshot : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    public NetworkRequestLocationRosterSnapshot(string settlementId)
    {
        SettlementId = settlementId;
    }
}
