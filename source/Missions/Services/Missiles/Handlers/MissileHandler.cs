using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Missions.Services.Agents.Handlers;
using Missions.Services.Agents.Packets;
using Missions.Services.Missiles.Message;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.MountAndBlade.Source.Objects.Siege.AgentPathNavMeshChecker;

namespace Missions.Services.Missiles.Handlers
{
    /// <summary>
    /// Handler for missiles within a battle
    /// </summary>
    public interface IMissileHandler : IHandler, IDisposable
    {

    }

    /// <inheritdoc/>
    public class MissileHandler : IMissileHandler
    {
        readonly INetworkMessageBroker networkMessageBroker;
        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly static ILogger Logger = LogManager.GetLogger<AgentDamageHandler>();

        public MissileHandler(INetworkMessageBroker networkMessageBroker, INetworkAgentRegistry networkAgentRegistry)
        {
            this.networkMessageBroker = networkMessageBroker;
            this.networkAgentRegistry = networkAgentRegistry;

            networkMessageBroker.Subscribe<AgentShoot>(AgentShootSend);
            networkMessageBroker.Subscribe<NetworkAgentShoot>(AgentShootRecieve);
        }

        ~MissileHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<AgentShoot>(AgentShootSend);
            networkMessageBroker.Unsubscribe<NetworkAgentShoot>(AgentShootRecieve);
        }

        private readonly static MethodInfo AddMissileSingleUsageAux = typeof(Mission).GetMethod("AddMissileSingleUsageAux", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static MethodInfo AddMissileAux = typeof(Mission).GetMethod("AddMissileAux", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static FieldInfo _missiles = typeof(Mission).GetField("_missiles", BindingFlags.NonPublic | BindingFlags.Instance);

        public readonly static Dictionary<NetPeer, Dictionary<int, int>> MissileIndexMap = new Dictionary<NetPeer, Dictionary<int, int>>();

        private void AgentShootRecieve(MessagePayload<NetworkAgentShoot> payload)
        {
            var peer = (NetPeer)payload.Who;
            if (networkAgentRegistry.TryGetGroupController(peer, out AgentGroupController agentGroupController) == false) return;

            if (MissileIndexMap.TryGetValue(peer, out var indexMap) == false){
                indexMap = new Dictionary<int, int>();
                MissileIndexMap.Add(peer, indexMap);
            }

            NetworkAgentShoot shot = payload.What;

            Agent shooter = agentGroupController.ControlledAgents[shot.AgentGuid];

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
                    shooter,
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

                GameLoopRunner.RunOnMainThread(() =>
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
                    shooter,
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

                GameLoopRunner.RunOnMainThread(() =>
                {
                    num = (int)AddMissileAux.Invoke(Mission.Current, parameters);
                }, true);

                missileEntity = (GameEntity)parameters.Last();
            }

            weaponData.DeinitializeManagedPointers();
            Mission.Missile missile1 = new Mission.Missile(Mission.Current, missileEntity);
            missile1.ShooterAgent = shooter;
            missile1.Weapon = missileWeapon;
            missile1.MissionObjectToIgnore = null;
            missile1.Index = num;
            Mission.Missile missile2 = missile1;

            var missiles = (Dictionary<int, Mission.Missile>)_missiles.GetValue(Mission.Current);

            missiles.Add(num, missile2);

            if(indexMap.TryGetValue(shot.MissileIndex, out int idx))
            {
                idx = num;
            }
            else
            {
                indexMap.Add(shot.MissileIndex, num);
            }

            //GameLoopRunner.RunOnMainThread(() =>
            //{
            //    Mission.Current.AddCustomMissile(
            //        shooter, 
            //        missileWeapon, 
            //        shot.Position, 
            //        shot.Velocity, 
            //        shot.Orientation, 
            //        shot.BaseSpeed,
            //        shot.Speed, 
            //        shot.HasRigidBody,
            //        null, 
            //        shot.MissileIndex);
            //});
        }

        private void AgentShootSend(MessagePayload<AgentShoot> payload)
        {  

            if (networkAgentRegistry.IsControlled(payload.What.Agent))
            {
                Guid shooterAgentGuid = networkAgentRegistry.AgentToId[payload.What.Agent];
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
                    shooterAgentGuid, 
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

                networkMessageBroker.PublishNetworkEvent(message);
            }
        }
    }
}
