using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Templates.ServiceTemplate.Messages
{
    [ProtoContract]
    public record NetworkMessageTemplate : IEvent
    {
    }
}
