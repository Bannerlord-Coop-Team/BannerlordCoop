using Common.Messaging;
using GameInterface.CoopSessionData.Save.Data;
using ProtoBuf;

namespace GameInterface.CoopSessionData.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateCoopSession : ICommand
{
    [ProtoMember(1)]
    public readonly CoopSession UpdatedSession;

    public NetworkUpdateCoopSession(CoopSession updatedSession)
    {
        UpdatedSession = updatedSession;
    }
}
