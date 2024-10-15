using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Audit;
[ProtoContract(SkipConstructor = true)]
internal record HeroAuditResponse : IAuditResponse<HeroAuditData>
{
    [ProtoMember(1)]
    public HeroAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    public HeroAuditResponse(HeroAuditData[] data, string serverAuditResult)
    {
        Data = data;
        ServerAuditResult = serverAuditResult;
    }
}
