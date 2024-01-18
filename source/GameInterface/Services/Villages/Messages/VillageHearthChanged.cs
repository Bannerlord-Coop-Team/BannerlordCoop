using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

public record VillageHearthChanged : ICommand
{
    public string VillageId { get; }
    public float Hearth { get; }
    public VillageHearthChanged(string villageID, float hearth)
    {
        VillageId = villageID;
        Hearth = hearth;
    }
}
