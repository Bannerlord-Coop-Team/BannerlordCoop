using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Save.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGameSaveStateChanged : IEvent
{
    [ProtoMember(1)]
    public readonly bool IsSaving;

    public NetworkGameSaveStateChanged(bool isSaving)
    {
        IsSaving = isSaving;
    }
}
