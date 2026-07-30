using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
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
                payload.Banner,
                payload.CurrentEquipment);

            network.SendAll(message);
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            GameThread.RunSafe(() =>
            {
                if (networkAgentRegistry.TryGetAgentInfo(obj.What.AgentId, out var agentInfo) == false)
                {
                    Logger.Warning("No agent found at {guid} in {class}", obj.What.AgentId, typeof(WeaponPickupHandler));
                    return;
                }

                Agent agent = agentInfo.Agent;
                if (agent == null || agent.Mission != Mission.Current || !agent.IsActive()) return;

                MissionWeapon missionWeapon = new MissionWeapon(
                    obj.What.ItemObject,
                    obj.What.ItemModifier,
                    obj.What.Banner);
                ApplyWeaponPickup(
                    agentInfo,
                    obj.What.EquipmentIndex,
                    ref missionWeapon,
                    obj.What.CurrentEquipment);
            });
        }

        internal static void ApplyWeaponPickup(
            CoopAgentInfo agentInfo,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon missionWeapon,
            AgentEquipmentData currentEquipment)
        {
            Agent agent = agentInfo.Agent;
            agentInfo.RecordAuthoritativeEquipment(currentEquipment);
            using (new AllowedThread())
            {
                if (equipmentIndex == EquipmentIndex.ExtraWeaponSlot)
                    agent.EquipWeaponToExtraSlotAndWield(ref missionWeapon);
                else
                    agent.EquipWeaponWithNewEntity(equipmentIndex, ref missionWeapon);

                // Vanilla chooses the hand after equipping a pickup. Replay that exact post-pickup state so a
                // shield does not remain holstered while the remote agent is already blocking with it.
                currentEquipment.Apply(agent);
            }
        }
    }
}
