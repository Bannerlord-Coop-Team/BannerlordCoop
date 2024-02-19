using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify client of HitPointsRatio set
/// </summary>
[ProtoContract(SkipConstructor =true)]
public record NetworkChangeWallHitPointsRatio : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int index { get; }
    [ProtoMember(3)]
    public float hitPointsRatio { get; }

    public NetworkChangeWallHitPointsRatio(string settlementId, int index, float hitPointsRatio)
    {
        SettlementId = settlementId;
        this.index = index;
        this.hitPointsRatio = hitPointsRatio;
    }
}
