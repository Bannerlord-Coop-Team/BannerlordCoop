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
        string nextCraftedItemId)
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

    public NetworkCreateCraftedWeaponInternalClients(
        string craftingCampaignBehaviorId,
        byte[] craftedItemObjectData,
        string nextCraftedItemId)
    {
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
        CraftedItemObjectData = craftedItemObjectData;
        NextCraftedItemId = nextCraftedItemId;
    }
}