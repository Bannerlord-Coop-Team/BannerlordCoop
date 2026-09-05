using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerCharacterChangedAfterHeirSelection : ICommand
{
    [ProtoMember(1)]
    public readonly string OldPlayerId;

    [ProtoMember(2)]
    public readonly string NewPlayerId;

    [ProtoMember(3)]
    public readonly string NewMainPartyId;

    [ProtoMember(4)]
    public readonly bool IsMainPartyChanged;

    public NetworkPlayerCharacterChangedAfterHeirSelection(
        string oldPlayerId,
        string newPlayerId,
        string newMainPartyId,
        bool isMainPartyChanged)
    {
        OldPlayerId = oldPlayerId;
        NewPlayerId = newPlayerId;
        NewMainPartyId = newMainPartyId;
        IsMainPartyChanged = isMainPartyChanged;
    }
}
