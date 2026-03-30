using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct CampaignVec2Surrogate
{
    [ProtoMember(1)]
    public float X { get; set; }

    [ProtoMember(2)]
    public float Y { get; set; }

    [ProtoMember(3)]
    public bool IsOnLand { get; set; }

    public CampaignVec2Surrogate(CampaignVec2 vec2)
    {
        X = vec2.X;
        Y = vec2.Y;
        IsOnLand = vec2.IsOnLand;
    }

    public static implicit operator CampaignVec2Surrogate(CampaignVec2 vec2)
    {
        return new CampaignVec2Surrogate(vec2);
    }

    public static implicit operator CampaignVec2(CampaignVec2Surrogate surrogate)
    {
        return new CampaignVec2(new Vec2(surrogate.X, surrogate.Y), surrogate.IsOnLand);
    }
}
