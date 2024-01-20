using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Template.Messages
{
    /// <summary>
    /// TODO describe what message does
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    // Name of the message should start with "Network"
    public record NetworkServerMessageTemplate : IEvent
    {
        [ProtoMember(1)]
        public string SomeData { get;  }

        public NetworkServerMessageTemplate(string someData)
        {
            SomeData = someData;
        }
    }
}
