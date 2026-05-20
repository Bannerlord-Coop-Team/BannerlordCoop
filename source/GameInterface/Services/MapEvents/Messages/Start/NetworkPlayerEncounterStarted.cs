using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Start;

public readonly struct NetworkPlayerEncounterStarted : ICommand
{
    public readonly string MapEventId;
    public readonly string MobilePartyId;

    public NetworkPlayerEncounterStarted(string mapEventId, string mobilePartyId)
    {
        MapEventId = mapEventId;
        MobilePartyId = mobilePartyId;
    }
}