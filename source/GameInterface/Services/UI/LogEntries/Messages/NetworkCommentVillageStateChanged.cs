using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.LogEntries.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommentVillageStateChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string VillageId;

    [ProtoMember(2)]
    public readonly Village.VillageStates OldState;

    [ProtoMember(3)]
    public readonly Village.VillageStates NewState;

    [ProtoMember(4)]
    public readonly string RaiderPartyId;

    public NetworkCommentVillageStateChanged(
        string villageId,
        Village.VillageStates oldState,
        Village.VillageStates newState,
        string raiderPartyId)
    {
        VillageId = villageId;
        OldState = oldState;
        NewState = newState;
        RaiderPartyId = raiderPartyId;
    }
}
