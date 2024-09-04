using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Template.Messages
{
    /// <summary>
    /// TODO describe what message does
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    // Name of the message should start with "Network"
    public record NetworkClientMessageTemplate : IEvent
    {
        [ProtoMember(1)]
        public string SomeData { get; }

        public NetworkClientMessageTemplate(string someData)
        {
            SomeData = someData;
        }
    }
}
