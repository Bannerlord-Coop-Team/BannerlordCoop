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
/// Authoritative time and party-position baseline for a joining client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkJoinCampaignBaseline : IMessage
{
    [ProtoMember(1)]
    public readonly long ServerTicks;

    [ProtoMember(2)]
    public readonly MobilePartyPositionData[] Positions;

    public NetworkJoinCampaignBaseline(long serverTicks, MobilePartyPositionData[] positions)
    {
        ServerTicks = serverTicks;
        Positions = positions;
    }
}
