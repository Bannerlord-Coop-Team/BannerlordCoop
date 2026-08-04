using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Messages;

/// <summary>Server -> all clients wire form of <see cref="CompanionRemovalAttempted"/>,
/// carrying the clan/companion by ID (rather than live reference) plus the original
/// <see cref="RemoveCompanionAction.RemoveCompanionDetail"/> reason, so
/// <see cref="Handlers.RemoveCompanionHandler"/> can resolve and apply the real removal on the
/// server.</summary>
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
