using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Owner → server (over the campaign network): one of the owner's battle agents became a casualty, so the
/// server applies it to the authoritative map-event roster and fans it out to clients via the existing
/// MapEventParty casualty sync. The host's own mission accounting is suppressed during a coop battle, so
/// this is the single source of battle casualties.
/// <para>
/// The casualty is keyed by the troop's <em>character</em> (StringId), not by a descriptor seed: the engine
/// re-flattens parties during battle setup (minting fresh descriptors), so a seed the owner captured at spawn
/// can be absent from the server roster — looking it up there threw KeyNotFoundException and silently dropped
/// enemy casualties. The server instead kills/wounds any live troop of this character with a current
/// descriptor (one of N identical troops is interchangeable for the head-count).
/// </para>
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkRequestBattleCasualty : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    /// <summary>StringId of the troop's <see cref="TaleWorlds.CampaignSystem.CharacterObject"/>.</summary>
    [ProtoMember(2)]
    public readonly string TroopCharacterId;
    /// <summary>True if the troop was wounded (fell unconscious) rather than killed outright.</summary>
    [ProtoMember(3)]
    public readonly bool Wounded;

    public NetworkRequestBattleCasualty(string mapEventPartyId, string troopCharacterId, bool wounded)
    {
        MapEventPartyId = mapEventPartyId;
        TroopCharacterId = troopCharacterId;
        Wounded = wounded;
    }
}
