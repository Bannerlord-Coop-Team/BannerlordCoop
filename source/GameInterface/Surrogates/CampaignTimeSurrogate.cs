using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct CampaignTimeSurrogate
{
    [ProtoMember(1)]
    public long Data { get; set; }

    public CampaignTimeSurrogate(CampaignTime campaignTime)
    {
        Data = campaignTime.NumTicks;
    }

    public static implicit operator CampaignTimeSurrogate(CampaignTime textObject)
    {
        return new CampaignTimeSurrogate(textObject);
    }

    public static implicit operator CampaignTime(CampaignTimeSurrogate surrogate)
    {
        return new CampaignTime(surrogate.Data);
    }
}