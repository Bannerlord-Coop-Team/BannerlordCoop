using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClearFocuses : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroDeveloperId;

    public NetworkClearFocuses(string heroDeveloperId)
    {
        HeroDeveloperId = heroDeveloperId;
    }
}
