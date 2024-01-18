using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

public record ChangeVillageHearth : ICommand
{
    public string VillageId { get; }
    public float Hearth { get; }

    public ChangeVillageHearth(string villageId, float hearth)
    {
        VillageId = villageId;
        Hearth = hearth;
    }
}
