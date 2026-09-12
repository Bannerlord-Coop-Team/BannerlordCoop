using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients of a siege engine's current hitpoints and max hitpoints.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSiegeEngineHitpoints : IEvent
{
    [ProtoMember(1)]
    public string SiegeEngineId { get; }
    [ProtoMember(2)]
    public float Hitpoints { get; }
    [ProtoMember(3)]
    public float MaxHitPoints { get; }

    public NetworkChangeSiegeEngineHitpoints(string siegeEngineId, float hitpoints, float maxHitPoints)
    {
        SiegeEngineId = siegeEngineId;
        Hitpoints = hitpoints;
        MaxHitPoints = maxHitPoints;
    }
}
