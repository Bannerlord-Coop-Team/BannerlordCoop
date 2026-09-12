using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyKingdomInfluenceChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    [ProtoMember(3)]
    public readonly string ClanId;

    [ProtoMember(4)]
    public readonly int GainedInfluence;

    [ProtoMember(5)]
    public readonly GainKingdomInfluenceAction.InfluenceGainingReason Detail;

    public NetworkNotifyKingdomInfluenceChanged(
        string heroId,
        string mobilePartyId,
        string clanId,
        int gainedInfluence,
        GainKingdomInfluenceAction.InfluenceGainingReason detail)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        ClanId = clanId;
        GainedInfluence = gainedInfluence;
        Detail = detail;
    }
}
