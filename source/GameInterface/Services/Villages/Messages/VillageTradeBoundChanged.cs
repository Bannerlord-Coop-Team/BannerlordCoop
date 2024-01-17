using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;
public record VillageTradeBoundChanged : IEvent
{
    public string VillageId { get; }
    public string TradeBoundId { get; }

    public VillageTradeBoundChanged(string villageId, string tradeBoundId)
    {
        VillageId = villageId;
        TradeBoundId = tradeBoundId;
    }
}
