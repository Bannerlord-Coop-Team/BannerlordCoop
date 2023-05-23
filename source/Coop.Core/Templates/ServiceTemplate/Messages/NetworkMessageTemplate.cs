using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Templates.ServiceTemplate.Messages
{
    [ProtoContract]
    public record NetworkMessageTemplate : IEvent
    {
    }
}
