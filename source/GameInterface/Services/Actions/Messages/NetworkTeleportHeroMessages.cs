using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct TeleportHero : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string TargetSettlementId;

    [ProtoMember(3)]
    public readonly string TargetPartyId;

    [ProtoMember(4)]
    public readonly TeleportHeroAction.TeleportationDetail Detail;

    public TeleportHero(
        string heroId,
        string targetSettlementId,
        string targetPartyId,
        TeleportHeroAction.TeleportationDetail detail)
    {
        HeroId = heroId;
        TargetSettlementId = targetSettlementId;
        TargetPartyId = targetPartyId;
        Detail = detail;
    }
}