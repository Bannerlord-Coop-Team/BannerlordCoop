using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Messages;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers
{
    /// <summary>
    /// Handler for shield breaks in a battle
    /// </summary>
    public interface IShieldDamageHandler : IHandler
    {

    }
    /// <inheritdoc/>
    public class ShieldDamageHandler : IShieldDamageHandler
    {
        private readonly INetworkAgentRegistry networkAgentRegistry;
        private readonly IBattleNetwork network;
        private readonly IMessageBroker messageBroker;
        readonly static ILogger Logger = LogManager.GetLogger<ShieldDamageHandler>();

        public ShieldDamageHandler(
            INetworkAgentRegistry networkAgentRegistry,
            IBattleNetwork network,
            IMessageBroker messageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.network = network;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<ShieldDamaged>(ShieldDamageSend);
            messageBroker.Subscribe<NetworkShieldDamaged>(ShieldDamageReceive);
        }
        ~ShieldDamageHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ShieldDamaged>(ShieldDamageSend);
            messageBroker.Unsubscribe<NetworkShieldDamaged>(ShieldDamageReceive);
        }

        private void ShieldDamageSend(MessagePayload<ShieldDamaged> payload)
        {

            if (!networkAgentRegistry.TryGetAgentInfo(payload.What.Agent, out var agentInfo))
                return;

            NetworkShieldDamaged message = new NetworkShieldDamaged(agentInfo.AgentId, payload.What.EquipmentIndex, payload.What.InflictedDamage);
            network.SendAll(message);
        }
        private void ShieldDamageReceive(MessagePayload<NetworkShieldDamaged> payload)
        {
            NetworkShieldDamaged message = payload.What;
            GameThread.RunSafe(() =>
            {
                if (!networkAgentRegistry.TryGetAgentInfo(message.AgentId, out var agentInfo))
                {
                    Logger.Warning("No agent found at {guid} in {class}", message.AgentId, typeof(ShieldDamageHandler));
                    return;
                }

                Mission mission = Mission.Current;
                Agent agent = agentInfo.Agent;
                if (mission == null || agent == null || agent.Mission != mission)
                {
                    Logger.Warning("Agent {guid} is not in the active mission in {class}", message.AgentId, typeof(ShieldDamageHandler));
                    return;
                }

                int slotIndex = (int)message.EquipmentIndex;
                if (slotIndex < 0 || slotIndex >= (int)EquipmentIndex.NumAllWeaponSlots ||
                    agent.Equipment[message.EquipmentIndex].IsEmpty)
                {
                    Logger.Warning("Equipment Index for {agent} is already empty or invalid in {class}", agent, typeof(ShieldDamageHandler));
                    return;
                }

                ApplyShieldDamage(agent, message.EquipmentIndex, message.InflictedDamage);
            }, context: nameof(ShieldDamageReceive));
        }

        internal static void ApplyShieldDamage(
            Agent agent,
            EquipmentIndex equipmentIndex,
            int inflictedDamage)
        {
            using (new AllowedThread())
            {
                agent.OnShieldDamaged(equipmentIndex, inflictedDamage);
            }
        }
    }
}
