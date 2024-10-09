using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CultureObjects.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateCultureObject : ICommand
    {
        [ProtoMember(1)]
        public string CultureObjectId { get; }

        public NetworkCreateCultureObject(string cultureObjectId)
        {
            CultureObjectId = cultureObjectId;
        }
    }
}
