using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Agents.Messages;
using Serilog;
using System.Reflection;
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
        private static readonly MethodInfo OnShieldDamaged = typeof(Agent).GetMethod("OnShieldDamaged", BindingFlags.NonPublic | BindingFlags.Instance);
        private void ShieldDamageReceive(MessagePayload<NetworkShieldDamaged> payload)
        {
            if (!networkAgentRegistry.TryGetAgentInfo(payload.What.AgentId, out var agentInfo))
            {
                Logger.Warning("No agent found at {guid} in {class}", payload.What.AgentId, typeof(ShieldDamageHandler));
                return;
            }

            var agent = agentInfo.Agent;
            if (agent.Equipment[payload.What.EquipmentIndex].IsEmpty)
            {
                Logger.Warning("Equipment Index for {agent} is already empty in {class}", agent, typeof(ShieldDamageHandler));
                return;
            }

            GameThread.RunSafe(() =>
            {
                OnShieldDamaged.Invoke(agent, new object[] { payload.What.EquipmentIndex, payload.What.InflictedDamage });
            });
        }
    }
}