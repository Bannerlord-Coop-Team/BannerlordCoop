using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Core.Server.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct MobilePartyPositionData
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly float X;

    [ProtoMember(3)]
    public readonly float Y;

    [ProtoMember(4)]
    public readonly bool IsOnLand;

    public MobilePartyPositionData(string mobilePartyId, CampaignVec2 position)
    {
        MobilePartyId = mobilePartyId;
        X = position.X;
        Y = position.Y;
        IsOnLand = position.IsOnLand;
    }

    public CampaignVec2 ToCampaignVec2() => new CampaignVec2(new Vec2(X, Y), IsOnLand);
}

/// <summary>
/// Sends current authoritative party positions immediately before a joining client enters the map.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkJoinMobilePartyPositions : IMessage
{
    [ProtoMember(1)]
    public readonly MobilePartyPositionData[] Positions;

    public NetworkJoinMobilePartyPositions(MobilePartyPositionData[] positions)
    {
        Positions = positions;
    }
}
