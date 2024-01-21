using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Used to change the Value of the Village's hearth.
/// </summary>
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
