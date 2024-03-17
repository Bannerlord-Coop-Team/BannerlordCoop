using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.Audit;
[ProtoContract(SkipConstructor = true)]
internal record HeroAuditResponse : IAuditResponse
{
    [ProtoMember(1)]
    public HeroAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    IEnumerable<IAuditData> IAuditResponse.Data => Data;

    public HeroAuditResponse(HeroAuditData[] data, string serverAuditResult)
    {
        Data = data;
        ServerAuditResult = serverAuditResult;
    }
}
