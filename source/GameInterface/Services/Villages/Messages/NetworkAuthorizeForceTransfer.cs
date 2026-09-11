using Common.Messaging;
using GameInterface.Services.Villages.Data;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAuthorizeForceTransfer : ICommand
{
    [ProtoMember(1)]
    public readonly ForceTransferPoolData Pool;

    public NetworkAuthorizeForceTransfer(ForceTransferPoolData pool)
    {
        Pool = pool;
    }
}
