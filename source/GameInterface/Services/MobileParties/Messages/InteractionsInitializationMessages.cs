using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;

public record InitializeClientInteractionsData : IEvent
{
    public InteractionsPlayerData InteractionsPlayerData;

    public InitializeClientInteractionsData(InteractionsPlayerData interactionsPlayerData)
    {
        InteractionsPlayerData = interactionsPlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerInteractionsDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerInteractionsDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}