using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

public record VillageHearthChanged : ICommand
{
    public string VillageID { get; }
    public float Hearth { get; }
    public VillageHearthChanged(string villageID, float hearth)
    {
        VillageID = villageID;
        Hearth = hearth;
    }
}
