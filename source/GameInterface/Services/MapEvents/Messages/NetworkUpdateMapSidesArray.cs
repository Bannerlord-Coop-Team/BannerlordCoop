using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;


[ProtoContract(SkipConstructor = true)]
internal record NetworkUpdateMapSidesArray(string InstanceId, string ValueId, int Index) : ICommand
{
    [ProtoMember(1)]
    public string InstanceId { get; } = InstanceId;

    [ProtoMember(2)]
    public string ValueId { get; } = ValueId;

    [ProtoMember(3)]
    public int Index { get; } = Index;
}
