using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;
[ProtoContract]
internal struct ItemCategorySurrogate
{
    [ProtoMember(1)]
    public bool IsTradeGood { get; set; }
    [ProtoMember(2)]
    public bool IsAnimal { get; set; }
    [ProtoIgnore]
    public ItemCategory CanSubstitute { get; set; }
    [ProtoMember(4)]
    public float SubstitutionFactor { get; set; }
    [ProtoMember(5)]
    public ItemCategory.Property Properties { get; set; }
    [ProtoMember(6)]
    public bool IsValid { get; set; }
    [ProtoMember(7)]
    public float BaseDemand { get; private set; }
    [ProtoMember(8)]
    public float LuxuryDemand { get; private set; }

    public ItemCategorySurrogate(ItemCategory ItemCategory)
    {
        if (ItemCategory == null)
        {
            IsTradeGood = false;
            IsAnimal = false;
            CanSubstitute = ObjectHelper.SkipConstructor<ItemCategory>();
            SubstitutionFactor = -1f;
            Properties = ItemCategory.Property.None;
            IsValid = false;
            BaseDemand = -1f;
            LuxuryDemand = -1f;
        }
        else
        {
            IsTradeGood = ItemCategory.IsTradeGood;
            IsAnimal = ItemCategory.IsAnimal;
            CanSubstitute = ItemCategory.CanSubstitute;
            SubstitutionFactor = ItemCategory.SubstitutionFactor;
            Properties = ItemCategory.Properties;
            IsValid = ItemCategory.IsValid;
            BaseDemand = ItemCategory.BaseDemand;
            LuxuryDemand = ItemCategory.LuxuryDemand;
        }
    }

    public static implicit operator ItemCategorySurrogate(ItemCategory item)
    {
        return new ItemCategorySurrogate(item);
    }

    public static implicit operator ItemCategory(ItemCategorySurrogate item)
    {
        return new ItemCategory();
    }
}
