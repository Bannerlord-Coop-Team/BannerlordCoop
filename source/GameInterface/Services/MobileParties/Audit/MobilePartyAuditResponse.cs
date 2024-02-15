using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Audit;

[ProtoContract(SkipConstructor = true)]
internal record MobilePartyAuditResponse : IEvent
{
    [ProtoMember(1)]
    public MobilePartyAuditData[] Data { get; }
    [ProtoMember(2)]
    public string ServerAuditResult { get; }

    public MobilePartyAuditResponse(MobilePartyAuditData[] data, string serverAuditResult)
    {
        Data = data;
        ServerAuditResult = serverAuditResult;
    }
}
