using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BasicCultureObjects.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateBasicCulture : ICommand
    {
        [ProtoMember(1)]
        public string CultureId { get; }

        public NetworkCreateBasicCulture(string cultureId)
        {
            CultureId = cultureId;
        }
    }
}
