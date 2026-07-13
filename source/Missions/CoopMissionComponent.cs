using Missions.Agents.Handlers;
using Missions.Missiles.Handlers;

namespace Missions;

public interface ICoopMissionComponent
{
    INetworkAgentRegistry AgentRegistry { get; }
    IMissileHandler MissileHandler { get; }
    IAgentMovementHandler AgentMovementHandler { get; }
    IAgentActionHandler AgentActionHandler { get; }
    IWeaponDropHandler WeaponDropHandler { get; }
    IWeaponPickupHandler WeaponPickupHandler { get; }
    IShieldDamageHandler ShieldDamageHandler { get; }
    IAgentDeathHandler AgentDeathHandler { get; }
}

public class CoopMissionComponent : ICoopMissionComponent
{
    public INetworkAgentRegistry AgentRegistry { get; }

    public IMissileHandler MissileHandler { get; }
    public IAgentMovementHandler AgentMovementHandler { get; }
    public IAgentActionHandler AgentActionHandler { get; }

    public IWeaponDropHandler WeaponDropHandler { get; }

    public IWeaponPickupHandler WeaponPickupHandler { get; }

    public IShieldDamageHandler ShieldDamageHandler { get; }

    public IAgentDeathHandler AgentDeathHandler { get; }

    public CoopMissionComponent(
        INetworkAgentRegistry agentRegistry,
        IMissileHandler missileHandler,
        IAgentMovementHandler agentMovementHandler,
        IAgentActionHandler agentActionHandler,
        IWeaponDropHandler weaponDropHandler,
        IWeaponPickupHandler weaponPickupHandler,
        IShieldDamageHandler shieldDamageHandler,
        IAgentDeathHandler agentDeathHandler
        )
    {
        AgentRegistry = agentRegistry;
        MissileHandler = missileHandler;
        AgentMovementHandler = agentMovementHandler;
        AgentActionHandler = agentActionHandler;
        WeaponDropHandler = weaponDropHandler;
        WeaponPickupHandler = weaponPickupHandler;
        ShieldDamageHandler = shieldDamageHandler;
        AgentDeathHandler = agentDeathHandler;
    }
}
