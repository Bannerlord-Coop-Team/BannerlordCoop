using Common.Messaging;
using GameInterface.Services.Villages.Data;

namespace GameInterface.Services.Villages.Messages;

public readonly struct ForceTransferOutcomeReady : IEvent
{
    public readonly ForceTransferPoolData Pool;

    public ForceTransferOutcomeReady(ForceTransferPoolData pool)
    {
        Pool = pool;
    }
}
