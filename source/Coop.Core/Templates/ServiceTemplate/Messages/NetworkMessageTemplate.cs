using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Templates.ServiceTemplate.Messages
{
    [ProtoContract]
    public record NetworkMessageTemplate : IEvent
    {
        [ProtoMember(1)]
        public string SomeData { get; } // TODO Rename/remove example data
        [ProtoMember(2)]
        public int SomeOtherData { get; } // TODO Rename/remove example data
    }
}
