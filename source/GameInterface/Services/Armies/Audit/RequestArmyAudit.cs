using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.Armies.Audit;

[ProtoContract(SkipConstructor = true)]
internal record RequestArmyAudit : ICommand
{
    [ProtoMember(1)]
    public ArmyAuditData[] Data { get; }

    public RequestArmyAudit(ArmyAuditData[] data)
    {
        Data = data;
    }
}
