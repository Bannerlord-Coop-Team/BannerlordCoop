using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

/// <summary>
/// Client → server: one of the sender's troops scored a hit; apply it to the authoritative
/// <c>MapEventParty</c>. The attacker is keyed by character because battle setup can replace its spawn-time
/// descriptor before the server applies the hit.
/// </summary>
[ProtoContract]
public readonly struct NetworkTroopScoreHit : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly string AttackingTroopId;
    [ProtoMember(3)]
    public readonly string AttackedTroopId;
    [ProtoMember(4)]
    public readonly int Damage;
    [ProtoMember(5)]
    public readonly bool IsFatal;
    [ProtoMember(6)]
    public readonly bool IsSimulatedHit;

    public NetworkTroopScoreHit(string mapEventPartyId, string attackingTroopId, string attackedTroopId, int damage, bool isFatal, bool isSimulatedHit)
    {
        MapEventPartyId = mapEventPartyId;
        AttackingTroopId = attackingTroopId;
        AttackedTroopId = attackedTroopId;
        Damage = damage;
        IsFatal = isFatal;
        IsSimulatedHit = isSimulatedHit;
    }
}
