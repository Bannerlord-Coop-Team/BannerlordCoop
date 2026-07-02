using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Host → battle mesh: the NPC AI has been released and the battle is live. Sent when the host activates on the
/// first deployment-finish (its own Start Battle, or a peer's). Clients record it so that, if one is later
/// promoted to host by migration while still deploying, it releases the NPCs it adopts instead of leaving them
/// frozen by the deployment AI gate. Non-hosts otherwise take no action — their NPCs are host-driven puppets.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleActivated : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkBattleActivated(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
