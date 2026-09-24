using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Used to change the Value of the Village's hearth.
/// </summary>
public record ChangeVillageHearth : ICommand
{
    public uint VillageId { get; }
    public float Hearth { get; }

    public ChangeVillageHearth(uint villageId, float hearth)
    {
        VillageId = villageId;
        Hearth = hearth;
    }
}
