using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCreateCraftedWeaponInternalServer : ICommand
{
    [ProtoMember(1)]
    public readonly bool IsFreeMode;

    [ProtoMember(2)]
    public readonly string CraftingHeroId;

    [ProtoMember(3)]
    public readonly TextObject Name;

    [ProtoMember(4)]
    public readonly string CultureId;

    [ProtoMember(5)]
    public readonly string CraftingTemplateId;

    [ProtoMember(6)]
    public readonly string WeaponName;

    [ProtoMember(7)]
    public readonly List<string> WeaponDesignElementCraftingPieceIds;

    [ProtoMember(8)]
    public readonly List<int> WeaponDesignElementScalePercentages;

    [ProtoMember(9)]
    public readonly string WeaponModifierId;

    [ProtoMember(10)]
    public readonly string PlayerHeroId;

    [ProtoMember(11)]
    public readonly string ItemModifierGroupId;

    public NetworkCreateCraftedWeaponInternalServer(
        bool isFreeMode,
        string craftingHeroId,
        TextObject name,
        string cultureId,
        string craftingTemplateId,
        string weaponName,
        List<string> weaponDesignElementCraftingPieceIds,
        List<int> weaponDesignElementScalePercentages,
        string weaponModifierId,
        string playerHeroId,
        string itemModifierGroupId)
    {
        IsFreeMode = isFreeMode;
        CraftingHeroId = craftingHeroId;
        Name = name;
        CultureId = cultureId;
        CraftingTemplateId = craftingTemplateId;
        WeaponName = weaponName;
        WeaponDesignElementCraftingPieceIds = weaponDesignElementCraftingPieceIds;
        WeaponDesignElementScalePercentages = weaponDesignElementScalePercentages;
        WeaponModifierId = weaponModifierId;
        PlayerHeroId = playerHeroId;
        ItemModifierGroupId = itemModifierGroupId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCreateCraftedWeaponInternalClients : ICommand
{
    [ProtoMember(1)]
    public readonly TextObject Name;

    [ProtoMember(2)]
    public readonly string CultureId;

    [ProtoMember(3)]
    public readonly string WeaponModifierId;

    [ProtoMember(4)]
    public readonly bool IsFreeMode;

    [ProtoMember(5)]
    public readonly string CraftingTemplateId;

    [ProtoMember(6)]
    public readonly string WeaponName;

    [ProtoMember(7)]
    public readonly List<string> WeaponDesignElementCraftingPieceIds;

    [ProtoMember(8)]
    public readonly List<int> WeaponDesignElementScalePercentages;

    [ProtoMember(9)]
    public readonly string ItemModifierGroupId;

    [ProtoMember(10)]
    public readonly string PlayerHeroId;

    [ProtoMember(11)]
    public readonly string NextCraftedItemId;

    public NetworkCreateCraftedWeaponInternalClients(NetworkCreateCraftedWeaponInternalServer cloneObject, string nextCraftedItemId)
    {
        Name = cloneObject.Name;
        CultureId = cloneObject.CultureId;
        WeaponModifierId = cloneObject.WeaponModifierId;
        IsFreeMode = cloneObject.IsFreeMode;
        CraftingTemplateId = cloneObject.CraftingTemplateId;
        WeaponName = cloneObject.WeaponName;
        WeaponDesignElementCraftingPieceIds = cloneObject.WeaponDesignElementCraftingPieceIds;
        WeaponDesignElementScalePercentages = cloneObject.WeaponDesignElementScalePercentages;
        ItemModifierGroupId = cloneObject.ItemModifierGroupId;
        PlayerHeroId = cloneObject.PlayerHeroId;
        NextCraftedItemId = nextCraftedItemId;
    }
}