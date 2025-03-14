using Common.Messaging;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using System.Collections.Generic;
using System.Reflection;
@UsingDeclarations@

namespace DynamicSync
{
    [ProtoContract(SkipConstructor = true)]
    public record @MessageType@ : GenericNetworkEvent<@ClassType@, @MemberType@>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public @MemberType@ Value { get; set; }

        [ProtoMember(3)]
        public int Length { get; set; }

        public @MessageType@(string instanceId, @MemberType@ value, int length) : base(instanceId)
        {
            Value = value;
            Length = length;
        }
    }
}
