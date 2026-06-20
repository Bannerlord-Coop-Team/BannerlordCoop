using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Agents.Messages;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers
{
    /// <summary>
    /// Handler for weapon pickups within a battle
    /// </summary>
    public interface IWeaponPickupHandler : IHandler
    {

    }
    /// <inheritdoc/>
    public class WeaponPickupHandler : IWeaponPickupHandler
    {
        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly IBattleNetwork network;
        readonly IMessageBroker messageBroker;
        readonly static ILogger Logger = LogManager.GetLogger<WeaponPickupHandler>();
        public WeaponPickupHandler(
            INetworkAgentRegistry networkAgentRegistry,
            IBattleNetwork network,
            IMessageBroker messageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.network = network;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Subscribe<NetworkWeaponPickedup>(WeaponPickupReceive);

        }
        ~WeaponPickupHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
        }

        private void WeaponPickupSend(MessagePayload<WeaponPickedup> obj)
        {
            var payload = obj.What;

            if (!networkAgentRegistry.IsLocallyControlled(payload.Agent))
                return;

            if (!networkAgentRegistry.TryGetAgentInfo(payload.Agent, out var agentInfo))
            {
                Logger.Warning("No agentID was found for the Agent: {agent}", payload.Agent);
                return;
            }

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(
                agentInfo.AgentId,
                payload.EquipmentIndex,
                payload.WeaponObject,
                payload.WeaponModifier,
                payload.Banner);

            network.SendAll(message);
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            //ItemObject - ItemModifier - Banner creates MissionWeapon
            MissionWeapon missionWeapon = new MissionWeapon(obj.What.ItemObject, obj.What.ItemModifier, obj.What.Banner);

            if (networkAgentRegistry.TryGetAgentInfo(obj.What.AgentId, out var agentInfo) == false)
            {
                Logger.Warning("No agent found at {guid} in {class}", obj.What.AgentId, typeof(WeaponPickupHandler));
                return;
            }

            var agent = agentInfo.Agent;

            GameThread.RunSafe(() =>
            {
                if (obj.What.EquipmentIndex == EquipmentIndex.ExtraWeaponSlot)
                {
                    agent.EquipWeaponToExtraSlotAndWield(ref missionWeapon);
                    return;
                }

                agent.EquipWeaponWithNewEntity(obj.What.EquipmentIndex, ref missionWeapon);
            });
        }
    }
}
