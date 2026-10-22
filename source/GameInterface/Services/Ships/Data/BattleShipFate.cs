using ProtoBuf;

namespace GameInterface.Services.Ships.Data;

/// <summary>
/// What a naval battle did to one campaign ship as its simulating client saw it: hull and sail HP, whether it sank, and the
/// damage its upgrades mitigated since the last report (the base of Boatswain XP).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class BattleShipFate
{
    [ProtoMember(1)] public readonly string CampaignShipId;
    [ProtoMember(2)] public readonly float HitPoints;
    [ProtoMember(3)] public readonly float SailHitPoints;
    [ProtoMember(4)] public readonly bool Sunk;
    [ProtoMember(5)] public readonly float MitigatedDamage;

    public BattleShipFate(string campaignShipId, float hitPoints, float sailHitPoints, bool sunk, float mitigatedDamage = 0f)
    {
        CampaignShipId = campaignShipId;
        HitPoints = hitPoints;
        SailHitPoints = sailHitPoints;
        Sunk = sunk;
        MitigatedDamage = mitigatedDamage;
    }

    /// <summary>Whether this fate's values are finite and its mitigated damage not negative, so the server can apply it.</summary>
    public bool IsValid => !string.IsNullOrEmpty(CampaignShipId) && IsFinite(HitPoints) && IsFinite(SailHitPoints)
        && IsFinite(MitigatedDamage) && MitigatedDamage >= 0f;

    /// <summary>This fate's condition with <paramref name="mitigatedDamage"/> as its mitigated damage.</summary>
    public BattleShipFate WithMitigatedDamage(float mitigatedDamage) =>
        new BattleShipFate(CampaignShipId, HitPoints, SailHitPoints, Sunk, mitigatedDamage);

    // NaN or infinity would poison the campaign ship.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
