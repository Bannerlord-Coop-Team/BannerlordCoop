using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCompanionRemovalAttempted : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string CompanionId;

    [ProtoMember(3)]
    public readonly RemoveCompanionAction.RemoveCompanionDetail Detail;

    public NetworkCompanionRemovalAttempted(string clanId, string companionId, RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        ClanId = clanId;
        CompanionId = companionId;
        Detail = detail;
    }
}
