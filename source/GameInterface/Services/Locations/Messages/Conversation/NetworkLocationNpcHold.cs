using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages.Conversation;

/// <summary>
/// Server → clients: a player was granted the conversation lock on a location NPC (SR-040). The NPC
/// host pauses its owned agent so the remote conversation anchors to a stationary NPC; everyone else
/// ignores it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkLocationNpcHold : IEvent
{
    [ProtoMember(1)]
    public readonly string LocationId;
    [ProtoMember(2)]
    public readonly string CharacterId;

    public NetworkLocationNpcHold(string locationId, string characterId)
    {
        LocationId = locationId;
        CharacterId = characterId;
    }
}

/// <summary>
/// Server → clients: the conversation lock on a location NPC was released (conversation ended or the
/// holder disconnected) — the NPC host un-pauses its agent (SR-040).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkLocationNpcReleased : IEvent
{
    [ProtoMember(1)]
    public readonly string LocationId;
    [ProtoMember(2)]
    public readonly string CharacterId;

    public NetworkLocationNpcReleased(string locationId, string characterId)
    {
        LocationId = locationId;
        CharacterId = characterId;
    }
}
