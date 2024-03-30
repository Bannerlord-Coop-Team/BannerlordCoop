using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.MobileParties.Audit;

[ProtoContract(SkipConstructor = true)]
internal record RequestMobilePartyAudit : IAuditRequest<MobilePartyAuditData>
{
    [ProtoMember(1)]
    public MobilePartyAuditData[] Data { get; }

    public RequestMobilePartyAudit(MobilePartyAuditData[] data)
    {
        Data = data;
    }
}
