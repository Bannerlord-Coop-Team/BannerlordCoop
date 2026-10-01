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

    [ProtoMember(3)] public readonly bool AppointClanLeader;

    public NetworkHeirSelectionOver(
        string originalHeroId,
        string selectedHeirId,
        bool appointClanLeader = false)
    {
        OriginalHeroId = originalHeroId;
        SelectedHeirId = selectedHeirId;
        AppointClanLeader = appointClanLeader;
    }
}
