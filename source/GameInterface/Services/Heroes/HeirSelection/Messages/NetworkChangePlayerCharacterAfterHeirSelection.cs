using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkChangePlayerCharacterAfterHeirSelection : ICommand
{
    [ProtoMember(1)]
    public readonly string HeirId;

    public NetworkChangePlayerCharacterAfterHeirSelection(string heirId)
    {
        HeirId = heirId;
    }
}
