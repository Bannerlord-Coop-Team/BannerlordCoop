using ProtoBuf;

namespace GameInterface.Services.Ships.Data;

/// <summary>
/// What a naval battle did to one campaign ship, as the client simulating its hull saw it: hull and sail HP, and whether it
/// sank, which vanilla treats as destroyed (Ship.OnShipDamaged destroys a ship at 0 HP).
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

    /// <summary>Whether this fate's values are finite, so the server can write them.</summary>
    public bool IsValid => !string.IsNullOrEmpty(CampaignShipId) && IsFinite(HitPoints) && IsFinite(SailHitPoints);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
