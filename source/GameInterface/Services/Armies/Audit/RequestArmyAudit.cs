using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.Armies.Audit;

/// <summary>
/// Command to request an army audit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record RequestArmyAudit : IAuditRequest<ArmyAuditData>
{
    [ProtoMember(1)]
    public ArmyAuditData[] Data { get; }

    public RequestArmyAudit(ArmyAuditData[] data)
    {
        Data = data;
    }
}
