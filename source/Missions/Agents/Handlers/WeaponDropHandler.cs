using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Missions.Agents.Messages;
using Serilog;

namespace GameInterface.Missions.Agents.Handlers;

/// <summary>
/// Handler for weapon drops in a battle
/// </summary>
public interface IWeaponDropHandler : IHandler
{

}
/// <inheritdoc/>
public class WeaponDropHandler : IWeaponDropHandler
{
    readonly static ILogger Logger = LogManager.GetLogger<WeaponDropHandler>();

    private readonly INetworkAgentRegistry networkAgentRegistry;
    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;

    public WeaponDropHandler(
        INetworkAgentRegistry networkAgentRegistry,
        IMessageBroker messageBroker,
        IBattleNetwork network)
    {
        this.networkAgentRegistry = networkAgentRegistry;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<WeaponDropped>(WeaponDropSend);
        messageBroker.Subscribe<NetworkWeaponDropped>(WeaponDropReceive);
        this.network = network;
    }

    ~WeaponDropHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WeaponDropped>(WeaponDropSend);
        messageBroker.Unsubscribe<NetworkWeaponDropped>(WeaponDropReceive);
    }

    private void WeaponDropSend(MessagePayload<WeaponDropped> obj)
    {
        

        if (!networkAgentRegistry.IsLocallyControlled(obj.What.Agent))
            return;

        if (!networkAgentRegistry.TryGetAgentInfo(obj.What.Agent, out var agentInfo))
        {
            Logger.Warning("No agentID was found for the Agent: {agent} in {class}", obj.What.Agent, typeof(WeaponDropHandler));
            return;
        }

        NetworkWeaponDropped message = new NetworkWeaponDropped(agentInfo.AgentId, obj.What.EquipmentIndex);

        network.SendAll(message);
    }

    private void WeaponDropReceive(MessagePayload<NetworkWeaponDropped> obj)
    { 
        if(networkAgentRegistry.TryGetAgentInfo(obj.What.AgentId, out var agentInfo) == false)
        {
            Logger.Warning("No agent found for {guid} in {class}", obj.What.AgentId, typeof(WeaponDropHandler));
            return;
        }

        var agent = agentInfo.Agent;
        if (agent.GetWeaponEntityFromEquipmentSlot(obj.What.EquipmentIndex) == null)
        {
            Logger.Error($"Tried to drop a weapon from an empty slot ({obj.What.EquipmentIndex})");
            return;
        }

        GameThread.Run(() =>
        {
            agent.DropItem(obj.What.EquipmentIndex);
        });
    }
}
