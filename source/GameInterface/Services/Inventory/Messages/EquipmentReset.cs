using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct EquipmentReset : IEvent
{
    public readonly Dictionary<CharacterObject, Equipment[]> CharacterEquipments;

    public EquipmentReset(Dictionary<CharacterObject, Equipment[]> characterEquipments)
    {
        CharacterEquipments = characterEquipments;
    }
}
