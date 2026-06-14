using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

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
    public byte[] CraftedItemObjectData;

    [ProtoMember(5)]
    public string CraftingTemplateId;

    [ProtoMember(6)]
    public string WeaponName;

    [ProtoMember(7)]
    public List<string> WeaponDesignElementCraftingPieceIds;

    [ProtoMember(8)]
    public List<int> WeaponDesignElementScalePercentages;

    [ProtoMember(9)]
    public string WeaponModifierId;

    [ProtoMember(10)]
    public string NextCraftedItemId;

    [ProtoMember(11)]
    public string PlayerHeroId;

    [ProtoMember(12)]
    public string ItemModifierGroupId;

    public NetworkCreateCraftedWeaponInternalServer(
        string craftingCampaignBehaviorId,
        bool isFreeMode,
        string craftingHeroId,
        byte[] craftedItemObjectData,
        string craftingTemplateId,
        string weaponName,
        List<string> weaponDesignElementCraftingPieceIds,
        List<int> weaponDesignElementScalePercentages,
        string weaponModifierId,
        string nextCraftedItemId,
        string playerHeroId,
        string itemModifierGroupId)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        IsFreeMode = isFreeMode;
        CraftingHeroId = craftingHeroId;
        CraftedItemObjectData = craftedItemObjectData;
        CraftingTemplateId = craftingTemplateId;
        WeaponName = weaponName;
        WeaponDesignElementCraftingPieceIds = weaponDesignElementCraftingPieceIds;
        WeaponDesignElementScalePercentages = weaponDesignElementScalePercentages;
        WeaponModifierId = weaponModifierId;
        NextCraftedItemId = nextCraftedItemId;
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
    public byte[] CraftedItemObjectData;

    [ProtoMember(3)]
    public string NextCraftedItemId;

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

    public NetworkCreateCraftedWeaponInternalClients(NetworkCreateCraftedWeaponInternalServer cloneObject)
    {
        CraftingCampaignBehaviorId = cloneObject.CraftingCampaignBehaviorId;
        CraftedItemObjectData = cloneObject.CraftedItemObjectData;
        NextCraftedItemId = cloneObject.NextCraftedItemId;
        WeaponModifierId = cloneObject.WeaponModifierId;
        IsFreeMode = cloneObject.IsFreeMode;
        CraftingTemplateId = cloneObject.CraftingTemplateId;
        WeaponName = cloneObject.WeaponName;
        WeaponDesignElementCraftingPieceIds = cloneObject.WeaponDesignElementCraftingPieceIds;
        WeaponDesignElementScalePercentages = cloneObject.WeaponDesignElementScalePercentages;
        ItemModifierGroupId = cloneObject.ItemModifierGroupId;
    }
}