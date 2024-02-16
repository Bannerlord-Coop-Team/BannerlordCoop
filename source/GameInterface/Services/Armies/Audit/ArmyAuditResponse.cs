using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Audit;

[ProtoContract(SkipConstructor = true)]
internal record ArmyAuditResponse : IEvent
{
    [ProtoMember(1)]
    public ArmyAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    public ArmyAuditResponse(ArmyAuditData[] data, string serverAuditResult)
    {
        Data = data;
        ServerAuditResult = serverAuditResult;
    }
}
