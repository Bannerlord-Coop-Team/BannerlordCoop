using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.MobileParties.Audit;

[ProtoContract(SkipConstructor = true)]
internal record RequestMobilePartyAudit : ICommand
{
    [ProtoMember(1)]
    public MobilePartyAuditData[] Data { get; }

    public RequestMobilePartyAudit(MobilePartyAuditData[] data)
    {
        Data = data;
    }
}
