using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAttackMissionAttempted : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    // The attacking client's main party. The dedicated server has no main party of its own,
    // so it needs the attacker's identity to apply the attack's hostile-action consequences
    // (war / relation) against the opposing side.
    [ProtoMember(2)]
    public readonly string AttackerPartyId;

    public NetworkAttackMissionAttempted(string mapEventId, string attackerPartyId)
    {
        MapEventId = mapEventId;
        AttackerPartyId = attackerPartyId;
    }
}
