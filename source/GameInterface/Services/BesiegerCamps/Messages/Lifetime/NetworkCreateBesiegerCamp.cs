using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BesiegerCamps.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateBesiegerCamp : ICommand
{
    [ProtoMember(1)]
    public string BesiegerCampId { get; }

    public NetworkCreateBesiegerCamp(string besiegerCampId)
    {
        BesiegerCampId = besiegerCampId;
    }
}
