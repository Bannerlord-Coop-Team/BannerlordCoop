using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Save.Messages;

/// <summary>Notifies clients when the authoritative server starts or finishes saving.</summary>
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
