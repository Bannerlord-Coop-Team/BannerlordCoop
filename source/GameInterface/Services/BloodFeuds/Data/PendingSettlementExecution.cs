using ProtoBuf;

namespace GameInterface.Services.BloodFeuds.Data;

[ProtoContract(SkipConstructor = true)]
public class PendingSettlementExecution
{
    [ProtoMember(1)]
    public string CapturerClanId { get; }

    [ProtoMember(2)]
    public long ExecutionDateTicks { get; }

    public PendingSettlementExecution(string capturerClanId, long executionDateTicks)
    {
        CapturerClanId = capturerClanId;
        ExecutionDateTicks = executionDateTicks;
    }
}
