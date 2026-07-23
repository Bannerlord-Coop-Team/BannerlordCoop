using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages;

[ProtoContract]
public readonly struct NetworkTroopRouted : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;
    [ProtoMember(2)]
    public readonly int TroopSeed;

    public NetworkTroopRouted(string mapEventPartyId, int troopSeed)
    {
        MapEventPartyId = mapEventPartyId;
        TroopSeed = troopSeed;
    }
}