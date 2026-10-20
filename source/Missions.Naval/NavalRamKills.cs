using GameInterface;
using NavalDLC.Missions.Objects;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Who a ram impact kills here: only agents this client controls or doesn't network, never another owner's
/// puppet, so every ram death happens on its owner and is reported once from there. Static because Harmony patches read it.
/// </summary>
internal static class NavalRamKills
{
    // MissionShipRam.OnTick kills every active agent within 2 m and breaks deck parts within 5 m of the impact (squared).
    internal const float KillRadiusSquared = 4f;
    internal const float DeckPartRadiusSquared = 25f;

    [System.ThreadStatic] private static bool inRamTick;

    /// <summary>Whether a ram's damage tick runs on this thread.</summary>
    internal static bool IsInRamTick => inRamTick;

    /// <summary>Marks the start of a ram's damage tick on this thread, so Agent.Die spares puppets until the exit.</summary>
    public static void EnterRamTick() => inRamTick = true;

    /// <summary>Marks the end of a ram's damage tick on this thread.</summary>
    public static void ExitRamTick() => inRamTick = false;

    /// <summary>Whether Agent.Die may run: always outside a ram's damage tick, inside it only for an agent this client may kill.</summary>
    public static bool AllowsDie(Agent agent)
    {
        if (!inRamTick || agent == null) return true;
        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agents)) return true;

        bool isRegistered = agents.TryGetAgentInfo(agent, out _);
        if (KillsAgent(isRegistered, isRegistered && agents.IsLocallyControlled(agent))) return true;

#if DEBUG
        NavalContactProbe.CountSparedRamPuppet(agent);
#endif
        return false;
    }

    /// <summary>Whether a ram impact kills an agent on this client: one it doesn't network or controls, never a puppet.</summary>
    internal static bool KillsAgent(bool isRegistered, bool isLocallyControlled) => !isRegistered || isLocallyControlled;

    /// <summary>
    /// A copy's ram hit something here: breaks the target's deck parts and kills this client's agents at the impact the way
    /// vanilla's tick does on the rammer's owner. Every client that sees the impact breaks the deck parts locally.
    /// </summary>
    public static void ApplyCopyImpact(MissionShipRam ram)
    {
        var impact = ram._ramDamageData;
        if (!impact.IsValid || impact.CalculatedDamage <= 0f) return;

        var point = impact.AverageIntersectionPoint;
        BreakDeckParts(ram, impact.TargetShip, point, impact.RamDirection);

        EnterRamTick();
        try
        {
            KillAgentsAt(point, BlowOwnerOf(ram._ownerShip));
        }
        finally
        {
            ExitRamTick();
        }
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

    // Vanilla's kill: a 200 blunt blow to every active agent within 2 m on the plane; Agent.Die spares puppets here.
    private static void KillAgentsAt(Vec3 point, Agent blowOwner)
    {
        var agents = Mission.Current?.Agents;
        if (agents == null) return;

        for (int i = agents.Count - 1; i >= 0; i--)
        {
            var agent = agents[i];
            var position = agent.Position;
            if (!agent.IsActive() || position.AsVec2.DistanceSquared(point.AsVec2) >= KillRadiusSquared) continue;

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

    // Vanilla credits the rammer's captain, or its helm pilot when that is the main agent or there is no captain.
    private static Agent BlowOwnerOf(MissionShip rammer)
    {
        var captain = rammer.Captain;
        var pilot = rammer.ShipControllerMachine?.PilotAgent;
        if (captain != null && (captain.IsMainAgent || pilot == null || !pilot.IsMainAgent)) return captain;

        return pilot;
    }
}
