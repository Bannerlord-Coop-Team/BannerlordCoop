using Common;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Missions.Services.Agents.Packets;
using Missions.Services.Missiles.Message;
using Missions.Services.Network;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Missiles.Handlers
{
    /// <summary>
    /// Handler for missiles within a battle
    /// </summary>
    public interface IMissileHandler : IHandler
    {

    }

    /// <inheritdoc/>
    public class MissileHandler : IMissileHandler
    {
        readonly INetworkMessageBroker networkMessageBroker;
        readonly INetworkAgentRegistry networkAgentRegistry;
        public MissileHandler(INetworkMessageBroker networkMessageBroker, INetworkAgentRegistry networkAgentRegistry)
        {
            this.networkMessageBroker = networkMessageBroker;
            this.networkAgentRegistry = networkAgentRegistry;

            networkMessageBroker.Subscribe<AgentShoot>(AgentShootSend);
            networkMessageBroker.Subscribe<NetworkAgentShoot>(AgentShootRecieve);
        }

        ~MissileHandler()
        {
            networkMessageBroker.Unsubscribe<AgentShoot>(AgentShootSend);
            networkMessageBroker.Unsubscribe<NetworkAgentShoot>(AgentShootRecieve);
        }

        private void AgentShootRecieve(MessagePayload<NetworkAgentShoot> payload)
        {
            networkAgentRegistry.TryGetGroupController(payload.Who as NetPeer, out AgentGroupController agentGroupController);


            NetworkAgentShoot shot = payload.What;

            Agent shooter = agentGroupController.ControlledAgents[shot.AgentGuid];
            MissionWeapon missionWeapon = new MissionWeapon(payload.What.ItemObject, payload.What.ItemModifier, payload.What.Banner);
            Mat3 orientation = new Mat3(shot.Orientations, shot.Orientationf, shot.Orientationu);

            GameLoopRunner.RunOnMainThread(() =>
            {
                Mission.Current.AddCustomMissile(shooter, missionWeapon, shot.Position, shot.Velocity, orientation, shot.BaseSpeed, shot.Speed, shot.HasRigidBody, null, shot.MissileIndex);
            });
        }

        private void AgentShootSend(MessagePayload<AgentShoot> payload)
        {  

            if (networkAgentRegistry.IsControlled(payload.What.Agent))
            {
                Guid shooterAgentGuid = networkAgentRegistry.AgentToId[payload.What.Agent];
                MissionWeapon missionWeapon;

                if (payload.What.MissionWeapon.CurrentUsageItem.IsRangedWeapon && payload.What.MissionWeapon.CurrentUsageItem.IsConsumable)
                {
                    missionWeapon = payload.What.MissionWeapon;

                }
                else
                {
                    missionWeapon = payload.What.MissionWeapon.AmmoWeapon;
                }

                NetworkAgentShoot message = new NetworkAgentShoot( 
                    shooterAgentGuid, 
                    payload.What.Position, 
                    payload.What.Direction, 
                    payload.What.Orientation.s, 
                    payload.What.Orientation.f, 
                    payload.What.Orientation.u, 
                    payload.What.HasRigidBody, 
                    missionWeapon.Item, 
                    missionWeapon.ItemModifier, 
                    missionWeapon.Banner, 
                    payload.What.MissileIndex, 
                    payload.What.BaseSpeed, 
                    payload.What.Speed);

                networkMessageBroker.PublishNetworkEvent(message);
            }
        }
    }
}
