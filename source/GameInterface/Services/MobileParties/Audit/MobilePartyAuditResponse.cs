using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties.Audit;

[ProtoContract(SkipConstructor = true)]
internal record MobilePartyAuditResponse : IAuditResponse
{
    [ProtoMember(1)]
    public MobilePartyAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    IEnumerable<IAuditData> IAuditResponse.Data => Data;

    public MobilePartyAuditResponse(MobilePartyAuditData[] data, string serverAuditResult)
    {
        Data = data;
        ServerAuditResult = serverAuditResult;
    }
}
