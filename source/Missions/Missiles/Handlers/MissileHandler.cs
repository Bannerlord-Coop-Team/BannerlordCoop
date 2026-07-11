using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Missiles.Message;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Handlers;

/// <summary>
/// Handler for missiles within a co-op mission
/// </summary>
public interface IMissileHandler : IHandler
{

}

/// <inheritdoc/>
public class MissileHandler : IMissileHandler
{
    readonly static ILogger Logger = LogManager.GetLogger<MissileHandler>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry networkAgentRegistry;

    public MissileHandler(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        INetworkAgentRegistry networkAgentRegistry)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.networkAgentRegistry = networkAgentRegistry;
        messageBroker.Subscribe<AgentShoot>(AgentShootSend);
        messageBroker.Subscribe<NetworkAgentShoot>(AgentShootRecieve);
    }

    ~MissileHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AgentShoot>(AgentShootSend);
        messageBroker.Unsubscribe<NetworkAgentShoot>(AgentShootRecieve);
    }

    private void AgentShootSend(MessagePayload<AgentShoot> payload)
    {
        if (!networkAgentRegistry.IsLocallyControlled(payload.What.Agent))
            return;

        if (!networkAgentRegistry.TryGetAgentInfo(payload.What.Agent, out var agentInfo))
        {
            Logger.Warning("No agentID was found for the Agent: {agent}", payload.What.Agent);
            return;
        }

        MissionWeapon missionWeapon;

        bool singleUse;
        if (payload.What.MissionWeapon.CurrentUsageItem.IsRangedWeapon &&
            payload.What.MissionWeapon.CurrentUsageItem.IsConsumable)
        {
            missionWeapon = payload.What.MissionWeapon;
            singleUse = true;
        }
        else
        {
            missionWeapon = payload.What.MissionWeapon.AmmoWeapon;
            singleUse = false;
        }

        Logger.Debug("Sending Agent Shoot with index {idx}", payload.What.MissileIndex);

        NetworkAgentShoot message = new NetworkAgentShoot(
            agentInfo.AgentId,
            payload.What.Position,
            payload.What.Direction,
            payload.What.Orientation,
            payload.What.HasRigidBody,
            missionWeapon.Item,
            missionWeapon.ItemModifier,
            missionWeapon.Banner,
            payload.What.MissileIndex,
            payload.What.BaseSpeed,
            payload.What.Speed,
            singleUse);

        network.SendAll(message);
    }

    private void AgentShootRecieve(MessagePayload<NetworkAgentShoot> payload)
    {
        NetworkAgentShoot shot = payload.What;

        GameThread.RunSafe(() =>
        {
            // The mission can tear down between the network receive and this deferred run.
            if (Mission.Current == null)
                return;

            if (!networkAgentRegistry.TryGetAgentInfo(shot.AgentId, out var agentInfo))
                return;

            Agent agent = agentInfo.Agent;

            Logger.Debug("Firing missile with id {id}", shot.MissileIndex);

            MissionWeapon missileWeapon = new MissionWeapon(shot.ItemObject, shot.ItemModifier, shot.Banner);
            WeaponData weaponData = missileWeapon.GetWeaponData(true);

            Vec3 position = shot.Position;
            Vec3 direction = shot.Velocity;
            Mat3 orientation = shot.Orientation;

            int index;
            GameEntity missileEntity;
            if (shot.SingleUse)
            {
                WeaponStatsData weaponStatsData = missileWeapon.GetWeaponStatsDataForUsage(0);
                index = Mission.Current.AddMissileSingleUsageAux(-1, false, agent, in weaponData, in weaponStatsData, 0f,
                    ref position, ref direction, ref orientation, shot.BaseSpeed, shot.Speed, shot.HasRigidBody,
                    WeakGameEntity.Invalid, false, out missileEntity);
            }
            else
            {
                WeaponStatsData[] weaponStatsData = missileWeapon.GetWeaponStatsData();
                index = Mission.Current.AddMissileAux(-1, false, agent, in weaponData, weaponStatsData, 0f,
                    ref position, ref direction, ref orientation, shot.BaseSpeed, shot.Speed, shot.HasRigidBody,
                    WeakGameEntity.Invalid, false, out missileEntity);
            }

            weaponData.DeinitializeManagedPointers();

            // A blocked or failed native add yields no entity, so there is nothing to render or track.
            if (missileEntity == null)
                return;

            // Track the missile in BOTH collections like vanilla OnAgentShootMissile: the engine hard-indexes
            // _missilesDictionary by the missile index on every collision, so a list-only add crashes on the first hit.
            // Indexer, not Add, so a reused engine index overwrites rather than throwing back out of the dictionary.
            Mission.Missile missile = new Mission.Missile(Mission.Current, index, missileEntity, agent, missileWeapon, null);
            Mission.Current._missilesList.Add(missile);
            Mission.Current._missilesDictionary[index] = missile;
        });
    }
}
