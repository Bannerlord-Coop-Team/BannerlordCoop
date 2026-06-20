using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct UpdateEquipmentClients : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, EquipmentData[]> CharacterIdEquipmentsData;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    [ProtoMember(3)]
    public readonly string InitialHeroId;

    public UpdateEquipmentClients(
        Dictionary<string, EquipmentData[]> characterIdEquipmentsData,
        string mobilePartyId,
        string initialHeroId)
    {
        CharacterIdEquipmentsData = characterIdEquipmentsData;
        MobilePartyId = mobilePartyId;
        InitialHeroId = initialHeroId;
    }
}