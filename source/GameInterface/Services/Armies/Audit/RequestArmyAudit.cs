using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.Armies.Audit;

/// <summary>
/// Command to request an army audit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record RequestArmyAudit : IAuditRequest
{
    [ProtoMember(1)]
    public ArmyAuditData[] Data { get; }

    IAuditData[] IAuditRequest.Data => Data;

    public RequestArmyAudit(ArmyAuditData[] data)
    {
        Data = data;
    }
}
