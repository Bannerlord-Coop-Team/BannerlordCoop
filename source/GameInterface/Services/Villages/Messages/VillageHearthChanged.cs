using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Used when the Hearth changes in a Village.
/// </summary>
/// 
[BatchLogMessage]
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
