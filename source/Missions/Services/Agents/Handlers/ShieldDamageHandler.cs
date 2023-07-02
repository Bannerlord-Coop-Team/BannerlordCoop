﻿using Common.Logging;
using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Serilog;
using Serilog.Core;
using System;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for shield breaks in a battle
    /// </summary>
    public interface IShieldDamageHandler : IHandler, IDisposable
    {

    }
    /// <inheritdoc/>
    public class ShieldDamageHandler : IShieldDamageHandler
    {
        private readonly INetworkAgentRegistry networkAgentRegistry;
        private readonly INetwork network;
        private readonly IMessageBroker messageBroker;
        readonly static ILogger Logger = LogManager.GetLogger<ShieldDamageHandler>();

        public ShieldDamageHandler(
            INetworkAgentRegistry networkAgentRegistry,
            INetwork network,
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

            if (networkAgentRegistry.TryGetAgentId(payload.What.Agent, out Guid agentId) == false) return;
            NetworkShieldDamaged message = new NetworkShieldDamaged(agentId, payload.What.EquipmentIndex, payload.What.InflictedDamage);
            network.SendAll(message);
        }
        private static readonly MethodInfo OnShieldDamaged = typeof(Agent).GetMethod("OnShieldDamaged", BindingFlags.NonPublic | BindingFlags.Instance);
        private void ShieldDamageReceive(MessagePayload<NetworkShieldDamaged> payload)
        {
            if (networkAgentRegistry.TryGetAgent(payload.What.AgentGuid, out Agent agent) == false)
            {
                Logger.Warning("No agent found at {guid} in {class}", payload.What.AgentGuid, typeof(ShieldDamageHandler));
                return;
            }

            if (agent.Equipment[payload.What.EquipmentIndex].IsEmpty)
            {
                Logger.Warning("Equipment Index for {agent} is already empty in {class}", agent, typeof(ShieldDamageHandler));
                return;
            }
            OnShieldDamaged.Invoke(agent, new object[] { payload.What.EquipmentIndex, payload.What.InflictedDamage });
        }
    }
}