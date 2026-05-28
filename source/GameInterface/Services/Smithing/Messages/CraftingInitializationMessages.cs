using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Smithing.Messages;

public record InitializeClientCraftingData : IEvent
{
    public CraftingPlayerData CraftingPlayerData;

    public InitializeClientCraftingData(CraftingPlayerData craftingPlayerData)
    {
        CraftingPlayerData = craftingPlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerCraftingDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerCraftingDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}