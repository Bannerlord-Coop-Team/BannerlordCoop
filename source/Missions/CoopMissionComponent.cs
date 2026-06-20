using GameInterface.Missions.Agents.Handlers;
using GameInterface.Missions.Missiles;
using GameInterface.Missions.Missiles.Handlers;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions;

public interface ICoopMissionComponent
{
    INetworkAgentRegistry AgentRegistry { get; }
    IMissileHandler MissileHandler { get; }
    IAgentMovementHandler AgentMovementHandler { get; }
    IWeaponDropHandler WeaponDropHandler { get; }
    IWeaponPickupHandler WeaponPickupHandler { get; }
    IShieldDamageHandler ShieldDamageHandler { get; }
    //IAgentDamageHandler AgentDamageHandler { get; }
    IAgentDeathHandler AgentDeathHandler { get; }
    //INetworkMissileRegistry NetworkMissileRegistry { get; }
}

public class CoopMissionComponent : ICoopMissionComponent
{
    public INetworkAgentRegistry AgentRegistry { get; }

    public IMissileHandler MissileHandler { get; }
    public IAgentMovementHandler AgentMovementHandler { get; }

    public IWeaponDropHandler WeaponDropHandler { get; }

    public IWeaponPickupHandler WeaponPickupHandler { get; }

    public IShieldDamageHandler ShieldDamageHandler { get; }

    //public IAgentDamageHandler AgentDamageHandler { get; }

    public IAgentDeathHandler AgentDeathHandler { get; }


    //public INetworkMissileRegistry NetworkMissileRegistry { get; }

    public CoopMissionComponent(
        INetworkAgentRegistry agentRegistry,
        IMissileHandler missileHandler,
        IAgentMovementHandler agentMovementHandler,
        IWeaponDropHandler weaponDropHandler,
        IWeaponPickupHandler weaponPickupHandler,
        IShieldDamageHandler shieldDamageHandler,
        //IAgentDamageHandler agentDamageHandler,
        IAgentDeathHandler agentDeathHandler
        //INetworkMissileRegistry networkMissileRegistry
        )
    {
        AgentRegistry = agentRegistry;
        MissileHandler = missileHandler;
        AgentMovementHandler = agentMovementHandler;
        WeaponDropHandler = weaponDropHandler;
        WeaponPickupHandler = weaponPickupHandler;
        ShieldDamageHandler = shieldDamageHandler;
        //AgentDamageHandler = agentDamageHandler;
        AgentDeathHandler = agentDeathHandler;
        //NetworkMissileRegistry = networkMissileRegistry;
    }
}
