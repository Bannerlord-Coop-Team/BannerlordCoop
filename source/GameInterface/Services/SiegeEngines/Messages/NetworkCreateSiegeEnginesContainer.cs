using Common.Messaging;

namespace GameInterface.Services.SiegeEnginesContainers.Messages;

internal class NetworkCreateSiegeEnginesContainer : ICommand
{
    public string SiegeEnginesId { get; }
    public string SiegeConstructionProgressId { get; }

    public NetworkCreateSiegeEnginesContainer(string siegeEnginesId, string siegeConstructionProgressId)
    {
        SiegeEnginesId = siegeEnginesId;
        SiegeConstructionProgressId = siegeConstructionProgressId;
    }
}