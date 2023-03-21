using Common;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Missiles.Message;
using Missions.Services.Missiles.Patches;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Missiles.Handlers
{
    public class MissileHandler
    {

        public MissileHandler()
        {
            NetworkMessageBroker.Instance.Subscribe<AgentShoot>(AgentShootSend);
            NetworkMessageBroker.Instance.Subscribe<NetworkAgentShoot>(AgentShootRecieve);
        }

        ~MissileHandler()
        {
            NetworkMessageBroker.Instance.Unsubscribe<AgentShoot>(AgentShootSend);
            NetworkMessageBroker.Instance.Unsubscribe<NetworkAgentShoot>(AgentShootRecieve);
        }

        private static readonly MethodInfo AddMissileAuxMethod = typeof(Mission).GetMethod("AddMissileAux", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo AddMissileSingleUsageAuxMethod = typeof(Mission).GetMethod("AddMissileSingleUsageAux", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _missiles = typeof(Mission).GetField("_missiles", BindingFlags.NonPublic | BindingFlags.Instance);

        private void AgentShootRecieve(MessagePayload<NetworkAgentShoot> payload)
        {
            NetworkAgentRegistry.Instance.TryGetGroupController(payload.Who as NetPeer, out AgentGroupController agentGroupController);


            NetworkAgentShoot shot = payload.What;

            Agent shooter = agentGroupController.ControlledAgents[shot.AgentGuid];
            MissionWeapon missionWeapon = new MissionWeapon(payload.What.ItemObject, payload.What.ItemModifier, payload.What.Banner);
            float baseSpeed = shooter.Equipment[shooter.GetWieldedItemIndex(Agent.HandIndex.MainHand)].GetModifiedMissileSpeedForCurrentUsage();
            float speed = shot.Velocity.Normalize();

            GameLoopRunner.RunOnMainThread(() =>
            {
                WeaponData weaponData = missionWeapon.GetWeaponData(true);
                GameEntity newMissile;

                if (missionWeapon.WeaponsCount == 1)
                {
                    WeaponStatsData weaponStatsData = missionWeapon.GetWeaponStatsDataForUsage(0);

                    object[] args = new object[] { shot.ForcedMissileIndex,
                        false,
                        shooter,
                        weaponData,
                        weaponStatsData,
                        0f,
                        shot.Position,
                        shot.Velocity,
                        shot.Orientation,
                        baseSpeed,
                        speed,
                        shot.HasRigidBody,
                        null,
                        true,
                        null
                    };

                    AddMissileSingleUsageAuxMethod.Invoke(Mission.Current, args);
                    newMissile = (GameEntity)args[14];
                }
                else
                {
                    WeaponStatsData[] weaponStatsData = missionWeapon.GetWeaponStatsData();

                    object[] args = new object[] { shot.ForcedMissileIndex,
                        false,
                        shooter,
                        weaponData,
                        weaponStatsData,
                        0f,
                        shot.Position,
                        shot.Velocity,
                        shot.Orientation,
                        baseSpeed,
                        speed,
                        shot.HasRigidBody,
                        null,
                        true,
                        null
                    };

                    AddMissileAuxMethod.Invoke(Mission.Current, args);
                    newMissile = (GameEntity)args[14];
                }

                Mission.Missile missile = new Mission.Missile(Mission.Current, newMissile)
                {
                    ShooterAgent = shooter,
                    Weapon = missionWeapon,
                    Index = payload.What.MissileIndex
                };

                newMissile.ManualInvalidate();

                Dictionary<int, Mission.Missile> missiles = (Dictionary<int, Mission.Missile>)_missiles.GetValue(Mission.Current);

                missiles.Add(missile.Index, missile);

                _missiles.SetValue(Mission.Current, missiles);

                foreach(MissionBehavior missionBehavior in Mission.Current.MissionBehaviors)
                {
                    missionBehavior.OnAgentShootMissile(shooter, shooter.GetWieldedItemIndex(Agent.HandIndex.MainHand), shot.Position, shot.Velocity, shot.Orientation, shot.HasRigidBody, shot.ForcedMissileIndex);
                }

                InformationManager.DisplayMessage(new InformationMessage(_missiles.ToString()));

            });
        }

        private void AgentShootSend(MessagePayload<AgentShoot> payload)
        {  

            if (NetworkAgentRegistry.Instance.IsControlled(payload.What.Agent))
            {
                InformationManager.DisplayMessage(new InformationMessage("Fired"));
                Guid shooterAgentGuid = NetworkAgentRegistry.Instance.AgentToId[payload.What.Agent];
                MissionWeapon missionWeapon;

                if (payload.What.Agent.WieldedWeapon.CurrentUsageItem.IsRangedWeapon && payload.What.Agent.WieldedWeapon.CurrentUsageItem.IsConsumable)
                {
                    missionWeapon = payload.What.Agent.WieldedWeapon;

                }
                else
                {
                    missionWeapon = payload.What.Agent.WieldedWeapon.AmmoWeapon;
                }

                NetworkAgentShoot message = new NetworkAgentShoot(shooterAgentGuid, payload.What.Position, payload.What.Direction, payload.What.Orientation, payload.What.HasRigidBody, payload.What.ForcedMissileIndex, missionWeapon.Item, missionWeapon.ItemModifier, missionWeapon.Banner, payload.What.MissileIndex);
                NetworkMessageBroker.Instance.PublishNetworkEvent(message);
            }
        }
    }
}
