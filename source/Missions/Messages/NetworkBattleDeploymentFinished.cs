using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Client → battle mesh: this client finished its own deployment (pressed Start Battle). The battle host
/// releases the NPC AI on the FIRST such announcement from ANY client — its own Start Battle included — so the
/// AI engages without waiting for every player to deploy ("NPC parties do not begin moving until any client has
/// finished deployment"). Non-host clients drive no NPCs (theirs are host-driven puppets), so they ignore it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleDeploymentFinished : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    public NetworkBattleDeploymentFinished(string controllerId)
    {
        ControllerId = controllerId;
    }
}
