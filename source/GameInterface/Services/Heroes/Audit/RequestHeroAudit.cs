using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.Heroes.Audit;

[ProtoContract(SkipConstructor = true)]
internal record RequestHeroAudit : IAuditRequest
{
    [ProtoMember(1)]
    public HeroAuditData[] Data { get; }

    IAuditData[] IAuditRequest.Data => Data;

    public RequestHeroAudit(HeroAuditData[] data)
    {
        Data = data;
    }
}
