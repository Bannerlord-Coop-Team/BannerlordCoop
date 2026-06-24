using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Owner → server (over the campaign network): one of the owner's battle agents became a casualty, so the
/// server applies it to the authoritative map-event roster and fans it out to clients via the existing
/// MapEventParty casualty sync. The host's own mission accounting is suppressed during a coop battle, so
/// this is the single source of battle casualties.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkRequestBattleCasualty : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly int TroopSeed;
    /// <summary>True if the troop was wounded (fell unconscious) rather than killed outright.</summary>
    [ProtoMember(3)]
    public readonly bool Wounded;

    public NetworkRequestBattleCasualty(string mapEventPartyId, int troopSeed, bool wounded)
    {
        MapEventPartyId = mapEventPartyId;
        TroopSeed = troopSeed;
        Wounded = wounded;
    }
}
