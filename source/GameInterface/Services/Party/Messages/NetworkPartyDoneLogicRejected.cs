using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPartyDoneLogicRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string Reason;

    public NetworkPartyDoneLogicRejected(string reason)
    {
        Reason = reason;
    }
}
