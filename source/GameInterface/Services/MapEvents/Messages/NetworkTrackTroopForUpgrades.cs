using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTrackTroopForUpgrades : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventPartyId;

    [ProtoMember(2)]
    public readonly string CharacterId;

    public NetworkTrackTroopForUpgrades(
        string mapEventPartyId,
        string characterId)
    {
        MapEventPartyId = mapEventPartyId;
        CharacterId = characterId;
    }
}
