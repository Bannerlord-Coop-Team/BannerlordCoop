using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CultureObjects.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateBasicCultureObject : ICommand
{
    [ProtoMember(1)]
    public string CultureObjectId { get; }

    public NetworkCreateBasicCultureObject(string cultureObjectId)
    {
        CultureObjectId = cultureObjectId;
    }
}
