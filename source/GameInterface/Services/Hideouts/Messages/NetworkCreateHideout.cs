using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Hideouts.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateHideout : ICommand
{
    [ProtoMember(1)]
    public string HideoutId { get; }
    [ProtoMember(2)]
    public uint Handle { get; }

    public NetworkCreateHideout(string hideoutId, uint handle)
    {
        HideoutId = hideoutId;
        Handle = handle;
    }
}
