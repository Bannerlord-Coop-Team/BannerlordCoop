using Common.Messaging;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>The frozen battle plan gained a party and active reserve authorities need a fresh snapshot.</summary>
public readonly struct BattleReserveScopeChanged : IEvent
{
    public readonly string MapEventId;

    public BattleReserveScopeChanged(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
