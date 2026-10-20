using GameInterface;
using NavalDLC.Missions.Objects;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] What a ram impact does here besides the hit: deck part breaks, and kills of only the agents this client
/// controls or doesn't network, so every ram death happens on its owner and is reported once. Static for the Harmony patch.
/// </summary>
internal static class NavalRamKills
{
    // MissionShipRam.OnTick kills every active agent within 2 m and breaks deck parts within 5 m of the impact (squared).
    internal const float KillRadiusSquared = 4f;
    internal const float DeckPartRadiusSquared = 25f;

    /// <summary>Whether a ram impact kills an agent on this client: one it doesn't network or controls, never a puppet.</summary>
    internal static bool KillsAgent(bool isRegistered, bool isLocallyControlled) => !isRegistered || isLocallyControlled;

    /// <summary>
    /// A ram hit something here with damage: breaks the target's deck parts and kills this client's agents at the impact as
    /// vanilla's tick does. Every client that sees the impact breaks the deck parts locally.
    /// </summary>
    public static void ApplyImpact(MissionShipRam ram)
    {
        var impact = ram._ramDamageData;
        var point = impact.AverageIntersectionPoint;
        BreakDeckParts(ram, impact.TargetShip, point, impact.RamDirection);
        KillAgentsAt(point, BlowOwnerOf(ram._ownerShip));
    }

    // Vanilla's deck part break: every intact destructible of the target within 5 m is destroyed by any hit.
    private static void BreakDeckParts(MissionShipRam ram, MissionShip target, Vec3 point, Vec3 direction)
    {
        foreach (var part in target.AllDestructableComponents)
        {
            if (part.IsDestroyed || part.GameEntity.GlobalPosition.DistanceSquared(point) >= DeckPartRadiusSquared) continue;

            part.DestroyOnAnyHit = true;
            part.TriggerOnHit(null, 1, point, direction, in MissionWeapon.Invalid, -1, ram);
        }
    }

    // Vanilla's kill: a 200 blunt blow to every active agent within 2 m on the plane, sparing another owner's puppet.
    private static void KillAgentsAt(Vec3 point, Agent blowOwner)
    {
        var agents = Mission.Current?.Agents;
        if (agents == null) return;

        ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry);
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            var agent = agents[i];
            var position = agent.Position;
            if (!agent.IsActive() || position.AsVec2.DistanceSquared(point.AsVec2) >= KillRadiusSquared) continue;
            if (!MayKill(registry, agent))
            {
#if DEBUG
                NavalContactProbe.CountSparedRamPuppet(agent);
#endif
                continue;
            }

            var blow = new Blow(blowOwner?.Index ?? agent.Index)
            {
                DamageType = DamageTypes.Blunt,
                BaseMagnitude = 200f,
                InflictedDamage = 200,
                GlobalPosition = position,
                DamagedPercentage = 1f,
            };
            agent.Die(blow);
        }
    }

    // Whether this client may kill the agent at a ram impact; without an agent registry nothing here is networked.
    private static bool MayKill(INetworkAgentRegistry registry, Agent agent)
    {
        if (registry == null) return true;

        bool isRegistered = registry.TryGetAgentInfo(agent, out _);
        return KillsAgent(isRegistered, isRegistered && registry.IsLocallyControlled(agent));
    }

    // Vanilla credits the rammer's captain, or its helm pilot when that is the main agent or there is no captain.
    private static Agent BlowOwnerOf(MissionShip rammer)
    {
        var captain = rammer.Captain;
        var pilot = rammer.ShipControllerMachine?.PilotAgent;
        if (captain != null && (captain.IsMainAgent || pilot == null || !pilot.IsMainAgent)) return captain;

        return pilot;
    }
}
