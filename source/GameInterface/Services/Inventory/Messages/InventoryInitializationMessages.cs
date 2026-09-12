using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Inventory.Messages;

public record InitializeClientInventoryData : IEvent
{
    public InventoryPlayerData InventoryPlayerData;

    public InitializeClientInventoryData(InventoryPlayerData inventoryPlayerData)
    {
        InventoryPlayerData = inventoryPlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerInventoryDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerInventoryDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}