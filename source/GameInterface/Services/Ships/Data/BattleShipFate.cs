using ProtoBuf;

namespace GameInterface.Services.Ships.Data;

/// <summary>
/// What a naval battle did to one campaign ship as its simulating client saw it: hull and sail HP and whether it sank.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class BattleShipFate
{
    [ProtoMember(1)] public readonly string CampaignShipId;
    [ProtoMember(2)] public readonly float HitPoints;
    [ProtoMember(3)] public readonly float SailHitPoints;
    [ProtoMember(4)] public readonly bool Sunk;

    public BattleShipFate(string campaignShipId, float hitPoints, float sailHitPoints, bool sunk)
    {
        CampaignShipId = campaignShipId;
        HitPoints = hitPoints;
        SailHitPoints = sailHitPoints;
        Sunk = sunk;
    }

    /// <summary>Whether this fate names a ship and its values are finite, so the server can apply it.</summary>
    public bool IsValid => !string.IsNullOrEmpty(CampaignShipId) && IsFinite(HitPoints) && IsFinite(SailHitPoints);

    // NaN or infinity would poison the campaign ship.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
