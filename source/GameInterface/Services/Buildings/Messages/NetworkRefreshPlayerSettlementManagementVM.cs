using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Buildings.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshPlayerSettlementManagementVM : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;

    public NetworkRefreshPlayerSettlementManagementVM(string townId)
    {
        TownId = townId;
    }
}
