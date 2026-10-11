using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// Local event raised on the server after a <see cref="Ship"/> constructor ran, so its constructor-only state replicates.
/// </summary>
internal readonly struct ShipInitialized : IEvent
{
    public readonly Ship Ship;

    public ShipInitialized(Ship ship)
    {
        Ship = ship;
    }
}

/// <summary>
/// The constructor-only state of a registered ship: its hull (which also fixes its upgrade slots), random value and hit points.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkInitializeShip : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;
    [ProtoMember(2)]
    public readonly uint HullHandle;
    [ProtoMember(3)]
    public readonly int RandomValue;
    [ProtoMember(4)]
    public readonly float HitPoints;
    [ProtoMember(5)]
    public readonly float SailHitPoints;

    public NetworkInitializeShip(uint shipHandle, uint hullHandle, int randomValue, float hitPoints, float sailHitPoints)
    {
        ShipHandle = shipHandle;
        HullHandle = hullHandle;
        RandomValue = randomValue;
        HitPoints = hitPoints;
        SailHitPoints = sailHitPoints;
    }
}
