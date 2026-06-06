using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct CampaignTimeSurrogate
{
    [ProtoMember(1)]
    public long NumberOfTicks { get; set; }

    public CampaignTimeSurrogate(CampaignTime campaignTime)
    {
        NumberOfTicks = campaignTime.NumTicks;
    }

    public static implicit operator CampaignTimeSurrogate(CampaignTime campaignTime)
    {
        return new CampaignTimeSurrogate(campaignTime);
    }

    public static implicit operator CampaignTime(CampaignTimeSurrogate surrogate)
    {
        return new CampaignTime(surrogate.NumberOfTicks);
    }
}