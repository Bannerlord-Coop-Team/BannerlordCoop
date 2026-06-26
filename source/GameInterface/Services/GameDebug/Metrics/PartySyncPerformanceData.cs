using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.GameDebug.Metrics;

[ProtoContract(SkipConstructor = true)]
public record PartySyncPerformanceData
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }

    [ProtoMember(3)]
    public float X { get; }

    [ProtoMember(4)]
    public float Y { get; }

    [ProtoIgnore]
    public CampaignVec2 Position => new CampaignVec2(new Vec2(X, Y), true);

    public PartySyncPerformanceData(string mobilePartyId, CampaignVec2 position)
    {
        MobilePartyId = mobilePartyId;
        X = position.X;
        Y = position.Y;
    }
}
