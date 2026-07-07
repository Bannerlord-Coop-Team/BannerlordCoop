using Common.Messaging;
using GameInterface.Services.Villages.Data;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageHostileActionDenied : ICommand
{
    [ProtoMember(1)]
    public readonly VillageHostileActionDeniedReason Reason;

    public NetworkVillageHostileActionDenied(VillageHostileActionDeniedReason reason)
    {
        Reason = reason;
    }
}
