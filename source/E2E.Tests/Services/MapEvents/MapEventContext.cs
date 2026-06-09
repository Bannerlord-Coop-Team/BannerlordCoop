using Moq;
using SandBox.GauntletUI.Map;

namespace E2E.Tests.Services.MapEvents;

public class MapEventContext
{
    public readonly string MapEventId;
    public readonly string AttackerPartyId;
    public readonly string DefenderPartyId;
    public MapEventContext(
        string mapEventId,
        string attackerPartyId,
        string defenderPartyId)
    {
        MapEventId = mapEventId;
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
    }
}
