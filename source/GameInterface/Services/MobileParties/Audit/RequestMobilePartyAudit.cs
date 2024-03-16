using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.MobileParties.Audit;

[ProtoContract(SkipConstructor = true)]
internal record RequestMobilePartyAudit : IAuditRequest
{
    [ProtoMember(1)]
    public MobilePartyAuditData[] Data { get; }

    IAuditData[] IAuditRequest.Data => Data;

    public RequestMobilePartyAudit(MobilePartyAuditData[] data)
    {
        Data = data;
    }
}
