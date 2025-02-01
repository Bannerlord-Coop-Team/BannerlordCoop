using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.ItemObjects.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateItemObject : ICommand
    {
        [ProtoMember(1)]
        public string ItemObjectId { get; set; }

        public NetworkCreateItemObject(string itemObjectId)
        {
            ItemObjectId = itemObjectId;
        }
    }
}
