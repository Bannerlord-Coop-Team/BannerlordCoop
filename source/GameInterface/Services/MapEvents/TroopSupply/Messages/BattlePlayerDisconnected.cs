using Common.Messaging;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>[Server] A campaign player's peer disconnected while its party remained in a battle.</summary>
public readonly struct BattlePlayerDisconnected : IEvent
{
    public readonly string MapEventId;
    public readonly string ControllerId;

    public BattlePlayerDisconnected(string mapEventId, string controllerId)
    {
        MapEventId = mapEventId;
        ControllerId = controllerId;
    }
}
