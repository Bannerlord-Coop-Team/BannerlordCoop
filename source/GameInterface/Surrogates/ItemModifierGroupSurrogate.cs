using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemModifierGroupSurrogate
{
    [ProtoMember(1)]
    public List<ItemModifier> ItemModifiers = new();
    [ProtoMember(2)]
    public int NoModifierLootScore { get; set; }
    [ProtoMember(3)]
    public int NoModifierProductionScore { get; set; }

    public ItemModifierGroupSurrogate(ItemModifierGroup itemModifierGroup)
    {
        ItemModifiers = itemModifierGroup?.ItemModifiers;

        if(itemModifierGroup != null)
        {
            NoModifierLootScore = itemModifierGroup.NoModifierLootScore;
            NoModifierProductionScore = itemModifierGroup.NoModifierProductionScore;
        }
        else
        {
            NoModifierLootScore = 0;
            NoModifierProductionScore = 0;
        }
    }

    public static implicit operator ItemModifierGroupSurrogate(ItemModifierGroup itemModifierGroup)
    {
        return new ItemModifierGroupSurrogate(itemModifierGroup);
    }

    public static implicit operator ItemModifierGroup(ItemModifierGroupSurrogate surrogate)
    {
        ItemModifierGroup itemModifierGroup = new ItemModifierGroup();
        
        if(surrogate.ItemModifiers == null) return itemModifierGroup;

        foreach(ItemModifier modifier in surrogate.ItemModifiers)
        {
            itemModifierGroup.ItemModifiers.Add(modifier);
        }

        itemModifierGroup.NoModifierLootScore = surrogate.NoModifierLootScore;
        itemModifierGroup.NoModifierProductionScore = surrogate.NoModifierProductionScore;

        return itemModifierGroup;
    }
}