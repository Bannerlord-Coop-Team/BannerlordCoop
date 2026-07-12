using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyKingdomInfluenceChanged : ICommand
{
    public readonly string HeroId;
    public readonly string MobilePartyId;
    public readonly string ClanId;
    public readonly int GainedInfluence;
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
