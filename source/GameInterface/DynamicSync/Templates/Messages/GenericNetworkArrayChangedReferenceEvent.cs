using Common.Messaging;
using GameInterface.Utils.NetworkEvents;
using ProtoBuf;
using System.Collections.Generic;
using System.Reflection;
@UsingDeclarations@

namespace DynamicSync
{
    [ProtoContract(SkipConstructor = true)]
    public record @MessageType@ : GenericNetworkReferenceEvent<@ClassType@, @MemberType@>
    {
        [ProtoMember(1)]
        public override string InstanceId { get; set; }

        [ProtoMember(2)]
        public override string ValueId { get; set; }

        [ProtoMember(3)]
        public int Index { get; set; }

        public @MessageType@(string instanceId, string valueId, int index) : base(instanceId, valueId)
        {
            Index = index;
        }
    }
}
