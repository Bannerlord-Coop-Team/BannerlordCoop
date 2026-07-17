using Missions.Agents.Handlers;
using Missions.Missiles.Handlers;

namespace Missions;

public interface ICoopMissionComponent
{
    INetworkAgentRegistry AgentRegistry { get; }
    IMissileHandler MissileHandler { get; }
    IAgentMovementHandler AgentMovementHandler { get; }
    IAgentActionHandler AgentActionHandler { get; }
    IAgentVoiceHandler AgentVoiceHandler { get; }
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
    public IAgentActionHandler AgentActionHandler { get; }
    public IAgentVoiceHandler AgentVoiceHandler { get; }

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
        IAgentActionHandler agentActionHandler,
        IAgentVoiceHandler agentVoiceHandler,
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
        AgentActionHandler = agentActionHandler;
        AgentVoiceHandler = agentVoiceHandler;
        WeaponDropHandler = weaponDropHandler;
        WeaponPickupHandler = weaponPickupHandler;
        ShieldDamageHandler = shieldDamageHandler;
        //AgentDamageHandler = agentDamageHandler;
        AgentDeathHandler = agentDeathHandler;
        //NetworkMissileRegistry = networkMissileRegistry;
    }
}
