using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemCategorySurrogate
{
    [ProtoMember(1)]
    public string StringId { get; set;   }
    [ProtoMember(2)]
    public bool IsTradeGood { get; set; }
    [ProtoMember(3)]
    public bool IsAnimal { get; set; }
    [ProtoMember(4)]
    public string CanSubstituteId { get; set; }
    [ProtoMember(5)]
    public float SubstitutionFactor { get;  set; }
    [ProtoMember(6)]
    public short Properties { get;  set; }
    [ProtoMember(7)]
    public bool IsValid { get;  set; }

    public ItemCategorySurrogate(ItemCategory itemCategory)
    {
        StringId = itemCategory.StringId;
        IsTradeGood = itemCategory.IsTradeGood;
        IsAnimal = itemCategory.IsAnimal;

        // item category
        CanSubstituteId = itemCategory.CanSubstitute.StringId;

        SubstitutionFactor = itemCategory.SubstitutionFactor;

        Properties = Convert.ToInt16(itemCategory.Properties);

        IsValid = itemCategory.IsValid;



    }

    public static implicit operator ItemCategorySurrogate(ItemCategory itemCategory)
    {
        return new ItemCategorySurrogate(itemCategory);
    }

    public static implicit operator ItemCategory(ItemCategorySurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (objectManager.TryGetObject<ItemCategory>(surrogate.StringId, out var itemCategory) == false) return null;

        if (objectManager.TryGetObject<ItemCategory>(surrogate.CanSubstituteId, out var canSub) == false) return null;

        itemCategory.CanSubstitute = canSub;

        itemCategory.SubstitutionFactor = surrogate.SubstitutionFactor;

        itemCategory.Properties = (ItemCategory.Property)surrogate.Properties;

        itemCategory.IsValid = surrogate.IsValid;

        return itemCategory;
    }
}
