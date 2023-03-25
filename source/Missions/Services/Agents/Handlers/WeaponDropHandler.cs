using Common.Messaging;
using Common.Network;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Patches;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    public class WeaponDropHandler
    {
        public WeaponDropHandler()
        {
            NetworkMessageBroker.Instance.Subscribe<WeaponDropped>(WeaponDropSend);
            NetworkMessageBroker.Instance.Subscribe<NetworkWeaponDropped>(WeaponDropRecieve);
        }

        ~WeaponDropHandler()
        {
            NetworkMessageBroker.Instance.Unsubscribe<WeaponDropped>(WeaponDropSend);
            NetworkMessageBroker.Instance.Unsubscribe<NetworkWeaponDropped>(WeaponDropRecieve);
        }

        public void WeaponDropSend(MessagePayload<WeaponDropped> obj)
        {
            NetworkAgentRegistry.Instance.TryGetAgentId(obj.What.Agent, out Guid agentId);

            NetworkWeaponDropped message = new NetworkWeaponDropped(agentId, obj.What.EquipmentIndex);

            NetworkMessageBroker.Instance.PublishNetworkEvent(message);
        }

        public void WeaponDropRecieve(MessagePayload<NetworkWeaponDropped> obj)
        { 
            NetworkAgentRegistry.Instance.TryGetAgent(obj.What.AgentGuid, out Agent agent);

            agent.DropItem(obj.What.EquipmentIndex);
        }
    }
}
