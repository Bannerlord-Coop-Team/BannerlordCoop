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

    public UpdateEquipmentClients(
        Dictionary<string, EquipmentData[]> characterIdEquipmentsData,
        string mobilePartyId)
    {
        CharacterIdEquipmentsData = characterIdEquipmentsData;
        MobilePartyId = mobilePartyId;
    }
}