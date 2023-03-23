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
                InformationManager.DisplayMessage(new InformationMessage(shot.MissileIndex.ToString()));
                Mission.Current.AddCustomMissile(shooter, missionWeapon, shot.Position, shot.Velocity, shot.Orientation, speed, speed, shot.HasRigidBody, null, shot.MissileIndex);

            });
        }

        private void AgentShootSend(MessagePayload<AgentShoot> payload)
        {  

            if (NetworkAgentRegistry.Instance.IsControlled(payload.What.Agent))
            {
                InformationManager.DisplayMessage(new InformationMessage("Fired"));
                Guid shooterAgentGuid = NetworkAgentRegistry.Instance.AgentToId[payload.What.Agent];
                MissionWeapon missionWeapon;

                if (payload.What.MissionWeapon.CurrentUsageItem.IsRangedWeapon && payload.What.MissionWeapon.CurrentUsageItem.IsConsumable)
                {
                    missionWeapon = payload.What.Agent.WieldedWeapon;

                }
                else
                {
                    missionWeapon = payload.What.MissionWeapon.AmmoWeapon;
                }

                NetworkAgentShoot message = new NetworkAgentShoot(shooterAgentGuid, payload.What.Position, payload.What.Direction, payload.What.Orientation, payload.What.HasRigidBody, missionWeapon.Item, missionWeapon.ItemModifier, missionWeapon.Banner, payload.What.MissileIndex);
                NetworkMessageBroker.Instance.PublishNetworkEvent(message);
            }
        }
    }
}
