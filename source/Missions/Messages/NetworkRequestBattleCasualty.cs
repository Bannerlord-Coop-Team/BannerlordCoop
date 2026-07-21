using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Owner → server (over the campaign network): one of the owner's battle agents became a casualty, so the
/// server applies it to the authoritative map-event roster and fans it out to clients via the existing
/// MapEventParty casualty sync. The host's own mission accounting is suppressed during a coop battle, so
/// this is the single source of battle casualties.
/// <para>
/// The server prefers the exact reserve seed, then falls back to the troop's character id when descriptor churn
/// means that seed no longer exists in its roster. The fallback still accounts one interchangeable troop.
/// </para>
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkRequestBattleCasualty : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    /// <summary>Object-manager id of the troop's <see cref="TaleWorlds.CampaignSystem.CharacterObject"/>.</summary>
    [ProtoMember(2)]
    public readonly string TroopCharacterId;
    /// <summary>True if the troop was wounded (fell unconscious) rather than killed outright.</summary>
    [ProtoMember(3)]
    public readonly bool Wounded;
    /// <summary>The exact mission-reserve seed that left the battle.</summary>
    [ProtoMember(4)]
    public readonly int TroopSeed;

    public NetworkRequestBattleCasualty(
        string mapEventPartyId,
        string troopCharacterId,
        bool wounded,
        int troopSeed)
    {
        MapEventPartyId = mapEventPartyId;
        TroopCharacterId = troopCharacterId;
        Wounded = wounded;
        TroopSeed = troopSeed;
    }
}
