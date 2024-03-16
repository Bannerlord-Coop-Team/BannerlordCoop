using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemModifierGroupSurrogate
{
    [ProtoMember(1)]
    public string StringId { get; set; }
    [ProtoMember(2)]
    public int NoModifierLootScore { get; set; }
    [ProtoMember(3)]
    public int NoModifierProductionScore { get; set; }

    public ItemModifierGroupSurrogate(ItemModifierGroup modifierGroup)
    {
        StringId = modifierGroup.StringId;

        NoModifierLootScore = modifierGroup.NoModifierLootScore;
        NoModifierProductionScore = modifierGroup.NoModifierProductionScore;
    }


    public static implicit operator ItemModifierGroupSurrogate(ItemModifierGroup modifierGroup)
    {
        return new ItemModifierGroupSurrogate(modifierGroup);
    }

    public static implicit operator ItemModifierGroup(ItemModifierGroupSurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (objectManager.TryGetObject<ItemModifierGroup>(surrogate.StringId, out var modifierGroup) == false) return null;

        modifierGroup.NoModifierLootScore = surrogate.NoModifierLootScore;
        modifierGroup.NoModifierProductionScore = surrogate.NoModifierProductionScore;


        return modifierGroup;
    }
}
