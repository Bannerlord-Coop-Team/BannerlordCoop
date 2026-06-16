using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Workshops.Messages;

public record InitializeClientWorkshopData : IEvent
{
    public WorkshopPlayerData WorkshopPlayerData;

    public InitializeClientWorkshopData(WorkshopPlayerData workshopPlayerData)
    {
        WorkshopPlayerData = workshopPlayerData;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkInitializeServerWorkshopDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerWorkshopDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}