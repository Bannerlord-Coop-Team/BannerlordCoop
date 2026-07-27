using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkCreateCraftedWeaponInternalServer : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public bool IsFreeMode;

    [ProtoMember(3)]
    public string CraftingHeroId;

    [ProtoMember(4)]
    public TextObject Name;

    [ProtoMember(5)]
    public string CultureId;

    [ProtoMember(6)]
    public string CraftingTemplateId;

    [ProtoMember(7)]
    public string WeaponName;

    [ProtoMember(8)]
    public List<string> WeaponDesignElementCraftingPieceIds;

    [ProtoMember(9)]
    public List<int> WeaponDesignElementScalePercentages;

    [ProtoMember(10)]
    public string WeaponModifierId;

    [ProtoMember(11)]
    public string PlayerHeroId;

    [ProtoMember(12)]
    public string ItemModifierGroupId;

    public NetworkCreateCraftedWeaponInternalServer(
        string craftingCampaignBehaviorId,
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
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
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
public class NetworkCreateCraftedWeaponInternalClients : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public TextObject Name;

    [ProtoMember(3)]
    public string CultureId;

    [ProtoMember(4)]
    public string WeaponModifierId;

    [ProtoMember(5)]
    public bool IsFreeMode;

    [ProtoMember(6)]
    public string CraftingTemplateId;

    [ProtoMember(7)]
    public string WeaponName;

    [ProtoMember(8)]
    public List<string> WeaponDesignElementCraftingPieceIds;

    [ProtoMember(9)]
    public List<int> WeaponDesignElementScalePercentages;

    [ProtoMember(10)]
    public string ItemModifierGroupId;

    [ProtoMember(11)]
    public string PlayerHeroId;

    [ProtoMember(12)]
    public string NextCraftedItemId;

    public NetworkCreateCraftedWeaponInternalClients(NetworkCreateCraftedWeaponInternalServer cloneObject, string nextCraftedItemId)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
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