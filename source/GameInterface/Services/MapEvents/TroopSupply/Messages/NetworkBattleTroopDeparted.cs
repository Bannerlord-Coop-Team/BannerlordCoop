using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.TroopSupply.Messages;

/// <summary>Owner to server: one exact supplied troop permanently left this battle without a roster casualty.</summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleTroopDeparted : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly int TroopSeed;

    public NetworkBattleTroopDeparted(string mapEventId, string partyId, int troopSeed)
    {
        MapEventId = mapEventId;
        PartyId = partyId;
        TroopSeed = troopSeed;
    }
}
