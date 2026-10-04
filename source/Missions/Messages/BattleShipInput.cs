using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// A hull's effective helm input (vanilla ShipInputRecord as integers). Replayed on a copied hull so its oars and sails
/// move like the owner's; the copy's body is not simulated, so the input produces no force there.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public struct BattleShipInput
{
    public BattleShipInput(int rowerLateral, int rowerLongitudinal, int rowerLongitudinalDoubleTap, float rudder, int sail)
    {
        RowerLateral = rowerLateral;
        RowerLongitudinal = rowerLongitudinal;
        RowerLongitudinalDoubleTap = rowerLongitudinalDoubleTap;
        Rudder = rudder;
        Sail = sail;
    }

    [ProtoMember(1)] public int RowerLateral { get; private set; }
    [ProtoMember(2)] public int RowerLongitudinal { get; private set; }
    [ProtoMember(3)] public int RowerLongitudinalDoubleTap { get; private set; }
    [ProtoMember(4)] public float Rudder { get; private set; }
    [ProtoMember(5)] public int Sail { get; private set; }

    public bool IsValid => !float.IsNaN(Rudder) && !float.IsInfinity(Rudder) && Math.Abs(Rudder) <= 1f;
}
