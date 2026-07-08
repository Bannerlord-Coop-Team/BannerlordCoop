using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

/// <summary>
/// Client → server: one of the sender's troops scored a hit; apply it to the authoritative
/// <c>MapEventParty</c>. Clients never apply this themselves — the resulting contribution reaches them
/// through the <c>MapEventParty._contributionToBattle</c> autosync and the xp through the roster sync.
/// </summary>
[ProtoContract]
public readonly struct NetworkTroopScoreHit : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly int TroopSeed;
    [ProtoMember(3)]
    public readonly string AttackedTroopId;
    [ProtoMember(4)]
    public readonly int Damage;
    [ProtoMember(5)]
    public readonly bool IsFatal;
    [ProtoMember(6)]
    public readonly bool IsSimulatedHit;

    public NetworkTroopScoreHit(string mapEventPartyId, int troopSeed, string attackedTroopId, int damage, bool isFatal, bool isSimulatedHit)
    {
        MapEventPartyId = mapEventPartyId;
        TroopSeed = troopSeed;
        AttackedTroopId = attackedTroopId;
        Damage = damage;
        IsFatal = isFatal;
        IsSimulatedHit = isSimulatedHit;
    }
}
