using Common;
using Common.PacketHandlers;
using Common.Util;
using LiteNetLib;
using Missions.Agents.Packets;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentEquipmentApplier : IPacketHandler
{
}

public class AgentEquipmentApplier : IAgentEquipmentApplier
{
    private readonly INetworkAgentRegistry agentRegistry;

    public AgentEquipmentApplier(INetworkAgentRegistry agentRegistry)
    {
        this.agentRegistry = agentRegistry;
    }

    public PacketType PacketType => PacketType.AgentEquipment;

    public void Dispose()
    {
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var equipment = (AgentEquipmentPacket)packet;
        int idCount = equipment.AgentIds?.Length ?? equipment.AgentGuids?.Length ?? 0;
        if (idCount == 0 || equipment.Equipment == null ||
            equipment.Equipment.Length != idCount)
        {
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            using (new AllowedThread())
            {
                for (int i = 0; i < idCount; i++)
                {
                    bool found = equipment.AgentIds != null
                        ? agentRegistry.TryGetAgentInfo(
                            equipment.IdentityScopeId, equipment.AgentIds[i], out var compactInfo)
                        : agentRegistry.TryGetAgentInfo(
                            equipment.AgentGuids[i], out compactInfo);
                    if (!found) continue;

                    Agent agent = compactInfo.Agent;
                    if (agent == null || agent.Mission != Mission.Current ||
                        !agent.IsActive() || agentRegistry.IsLocallyControlled(agent))
                    {
                        continue;
                    }

                    equipment.Equipment[i].Apply(agent);
                }
            }
        });
    }
}
