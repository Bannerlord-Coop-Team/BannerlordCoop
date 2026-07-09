using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyHostileEncounterStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string AttackerPartyId;
    [ProtoMember(3)]
    public readonly string DefenderPartyId;
    [ProtoMember(4)]
    public readonly string MapEventId;

    public NetworkPlayerPartyHostileEncounterStarted(
        string sessionId,
        string attackerPartyId,
        string defenderPartyId,
        string mapEventId)
    {
        SessionId = sessionId;
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
        MapEventId = mapEventId;
    }
}
