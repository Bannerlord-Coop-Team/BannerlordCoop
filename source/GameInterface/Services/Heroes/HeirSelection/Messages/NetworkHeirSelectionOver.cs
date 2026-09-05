using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeirSelectionOver : ICommand
{
    [ProtoMember(1)]
    public readonly string OriginalHeroId;

    [ProtoMember(2)]
    public readonly string SelectedHeirId;

    public NetworkHeirSelectionOver(
        string originalHeroId,
        string selectedHeirId)
    {
        OriginalHeroId = originalHeroId;
        SelectedHeirId = selectedHeirId;
    }
}
