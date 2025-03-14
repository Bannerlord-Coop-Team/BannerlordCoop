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

        public @MessageType@(string instanceId, @MemberType@ value) : base(instanceId)
        {
            Value = value;
        }
    }
}
