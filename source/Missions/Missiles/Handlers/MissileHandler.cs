using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Missions.Missiles.Message;
using GameInterface.Missions.Services.Network;
using Serilog;
using System.Linq;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Missiles.Handlers;

/// <summary>
/// Handler for missiles within a battle
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

    private readonly static MethodInfo AddMissileSingleUsageAux = typeof(Mission).GetMethod("AddMissileSingleUsageAux", BindingFlags.NonPublic | BindingFlags.Instance);
    private readonly static MethodInfo AddMissileAux = typeof(Mission).GetMethod("AddMissileAux", BindingFlags.NonPublic | BindingFlags.Instance);

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
        if (!networkAgentRegistry.TryGetAgentInfo(payload.What.AgentId, out var agentInfo))
            return;

        NetworkAgentShoot shot = payload.What;
        var agent = agentInfo.Agent;

        Logger.Debug("Firing missile with id {id}", shot.MissileIndex);

        MissionWeapon missileWeapon = new MissionWeapon(
            shot.ItemObject,
            shot.ItemModifier,
            shot.Banner);

        WeaponData weaponData = missileWeapon.GetWeaponData(true);

        GameEntity missileEntity = null;

        int num = 0;

        if (shot.SingleUse)
        {
            WeaponStatsData weaponStatsData = missileWeapon.GetWeaponStatsDataForUsage(0);
            var parameters = new object[]
            {
                -1,
                false,
                agent,
                weaponData,
                weaponStatsData,
                0.0f,
                shot.Position,
                shot.Velocity,
                shot.Orientation,
                shot.BaseSpeed,
                shot.Speed,
                shot.HasRigidBody,
                null,
                false,
                null,
            };

            GameThread.Run(() =>
            {
                num = (int)AddMissileSingleUsageAux.Invoke(Mission.Current, parameters);
            }, true);

            missileEntity = (GameEntity)parameters.Last();
        }
        else
        {
            WeaponStatsData[] weaponStatsData = missileWeapon.GetWeaponStatsData();

            var parameters = new object[]
            {
                -1,
                false,
                agent,
                weaponData,
                weaponStatsData,
                0.0f,
                shot.Position,
                shot.Velocity,
                shot.Orientation,
                shot.BaseSpeed,
                shot.Speed,
                shot.HasRigidBody,
                null,
                false,
                null,
            };

            GameThread.RunSafe(() =>
            {
                num = (int)AddMissileAux.Invoke(Mission.Current, parameters);
            }, true);

            missileEntity = (GameEntity)parameters.Last();
        }

        weaponData.DeinitializeManagedPointers();
        Mission.Missile missile = new Mission.Missile(Mission.Current, num, missileEntity, agent, missileWeapon, null); // Probably need to change this to not be null

        missileEntity.ManualInvalidate();

        var missiles = Mission.Current._missilesList;

        missiles.Add(missile);
    }
}
