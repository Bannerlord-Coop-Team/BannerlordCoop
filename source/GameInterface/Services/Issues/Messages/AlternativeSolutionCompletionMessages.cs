using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAlternativeSolutionCompletion : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestAlternativeSolutionCompletion(string ownerId)
    {
        OwnerId = ownerId;
    }
}
