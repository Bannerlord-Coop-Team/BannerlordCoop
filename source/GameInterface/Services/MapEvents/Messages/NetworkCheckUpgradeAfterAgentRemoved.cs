using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCheckUpgradeAfterAgentRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly string PartyId;

    [ProtoMember(3)]
    public readonly string CharacterObjectId;

    [ProtoMember(4)]
    public readonly BattleSideEnum Side;

    public NetworkCheckUpgradeAfterAgentRemoved(
        string mapEventId,
        string partyId,
        string characterObjectId,
        BattleSideEnum side)
    {
        MapEventId = mapEventId;
        PartyId = partyId;
        CharacterObjectId = characterObjectId;
        Side = side;
    }
}
