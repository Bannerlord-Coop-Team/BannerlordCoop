using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemObjectSurrogate
{
    [ProtoMember(1)]
    public string StringId { get; set; }

    [ProtoMember(2)]
    public ItemComponent ItemComponent { get; set; }
    [ProtoMember(3)]
    public string MultiMeshName { get; set; }

    [ProtoMember(4)]
    public string HolsterMeshName { get; set; }
    [ProtoMember(5)]
    public string HolsterWithWeaponMeshName { get; set; }
    [ProtoMember(6)]
    public string[] ItemHolsters { get; set; }
    [ProtoMember(7)]
    public Vec3 HolsterPositionShift { get; set; }
    [ProtoMember(8)]
    public bool HasLowerHolsterPriority { get; set; }
    [ProtoMember(9)]
    public string FlyingMeshName { get; set; }
    [ProtoMember(10)]
    public string BodyName { get; set;  }
    [ProtoMember(11)]
    public string HolsterBodyName { get; set; }
    [ProtoMember(12)]
    public string CollisionBodyName { get; set; }
    [ProtoMember(13)]
    public bool RecalculateBody { get; set; }
    [ProtoMember(14)]
    public string PrefabName { get; set; }
    [ProtoMember(15)]
    public TextObject Name { get; set; }
    [ProtoMember(16)]
    public short ItemFlags { get; set; }
    [ProtoMember(17)]
    public ItemCategory ItemCategory { get; set; }
    [ProtoMember(18)]
    public int Value {  get; set; }
    [ProtoMember(19)]
    public float Effectiveness { get; set;  }
    [ProtoMember(20)]
    public float Weight { get; set;  }
    [ProtoMember(21)]
    public int Difficulty { get; set; }
    [ProtoMember(22)]
    public float Appearance { get; set; }
    [ProtoMember(23)]
    public bool IsUsingTableau { get; set; }
    [ProtoMember(24)]
    public string ArmBandMeshName { get; set;  }
    [ProtoMember(25)]
    public bool IsFood { get; set; }
    [ProtoMember(26)]
    public bool IsUniqueItem { get; set; }
    [ProtoMember(27)]
    public float ScaleFactor { get; set; }
    [ProtoMember(28)]
    public BasicCultureObject Culture { get; set; }
    [ProtoMember(29)]
    public bool MultiplayerItem { get; set; }
    [ProtoMember(30)]
    public bool NotMerchandise { get; set; }
    [ProtoMember(31)]
    public bool IsCraftedByPlayer { get; set; }
    [ProtoMember(32)]
    public int LodAtlasIndex { get; set; }
    [ProtoMember(33)]
    public WeaponDesign WeaponDesign { get; set; }
    [ProtoMember(34)]
    public short Type { get; set; }

    [ProtoMember(35)]
    public string PrerequisiteItem { get; set; }

    public ItemObjectSurrogate(ItemObject obj)
    {
        StringId = obj.StringId;

        //ITEMCOMPONENTSURROGATE
        ItemComponent = obj.ItemComponent;

        MultiMeshName = obj.MultiMeshName;
        HolsterMeshName = obj.HolsterMeshName;
        HolsterWithWeaponMeshName = obj.HolsterWithWeaponMeshName;
        ItemHolsters = obj.ItemHolsters;

        // VEC3SURROGATE
        HolsterPositionShift = obj.HolsterPositionShift;

        HasLowerHolsterPriority = obj.HasLowerHolsterPriority;
        FlyingMeshName = obj.FlyingMeshName;
        BodyName = obj.BodyName;
        HolsterBodyName = obj.HolsterBodyName;
        CollisionBodyName = obj.CollisionBodyName;
        RecalculateBody = obj.RecalculateBody;
        PrefabName = obj.PrefabName;

        // TEXTOBJECTSURROGATE
        Name = obj.Name;

        ItemFlags = Convert.ToInt16(obj.ItemFlags);

        // ITEMCATEGORYSURROGATE
        ItemCategory = obj.ItemCategory;

        Value = obj.Value;
        Effectiveness = obj.Effectiveness;
        Weight = obj.Weight;
        Difficulty = obj.Difficulty;
        Appearance = obj.Appearance;
        IsUsingTableau = obj.IsUsingTableau;
        ArmBandMeshName = obj.ArmBandMeshName;
        IsFood = obj.IsFood;
        IsUniqueItem = obj.IsUniqueItem;
        ScaleFactor = obj.ScaleFactor;

        // CultureObjectSurrogate 
        Culture = obj.Culture;

        MultiplayerItem = obj.MultiplayerItem;
        NotMerchandise = obj.NotMerchandise;
        IsCraftedByPlayer = obj.IsCraftedByPlayer;
        LodAtlasIndex = obj.LodAtlasIndex;

        // WeaponDesignSurrogate
        WeaponDesign = obj.WeaponDesign;

        Type = Convert.ToInt16(obj.Type);

        // Needs to be converted later
        PrerequisiteItem = obj.PrerequisiteItem.StringId;
    }

    public static implicit operator ItemObjectSurrogate(ItemObject obj)
    {
        return new ItemObjectSurrogate(obj);
    }

    public static implicit operator ItemObject(ItemObjectSurrogate obj)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (objectManager.TryGetObject<ItemObject>(obj.StringId, out var itemObject) == false) return null;

        if (objectManager.TryGetObject<ItemObject>(obj.PrerequisiteItem, out var prequisteItem) == false) return null;


        //ITEMCOMPONENTSURROGATE
        itemObject.ItemComponent = obj.ItemComponent;

        itemObject.MultiMeshName = obj.MultiMeshName;
        itemObject.HolsterMeshName = obj.HolsterMeshName;
        itemObject.HolsterWithWeaponMeshName = obj.HolsterWithWeaponMeshName;
        itemObject. ItemHolsters = obj.ItemHolsters;

        // VEC3SURROGATE
        itemObject.HolsterPositionShift = obj.HolsterPositionShift;

        itemObject.HasLowerHolsterPriority = obj.HasLowerHolsterPriority;
        itemObject.FlyingMeshName = obj.FlyingMeshName;
        itemObject.BodyName = obj.BodyName;
        itemObject.HolsterBodyName = obj.HolsterBodyName;
        itemObject.CollisionBodyName = obj.CollisionBodyName;
        itemObject.RecalculateBody = obj.RecalculateBody;
        itemObject.PrefabName = obj.PrefabName;

        // TEXTOBJECTSURROGATE
        itemObject.Name = obj.Name;

        itemObject.ItemFlags = (ItemFlags)obj.ItemFlags;

        // ITEMCATEGORYSURROGATE
        itemObject.ItemCategory = obj.ItemCategory;

        itemObject.Value = obj.Value;
        itemObject.Effectiveness = obj.Effectiveness;
        itemObject.Weight = obj.Weight;
        itemObject.Difficulty = obj.Difficulty;
        itemObject.Appearance = obj.Appearance;
        itemObject.IsUsingTableau = obj.IsUsingTableau;
        itemObject.ArmBandMeshName = obj.ArmBandMeshName;
        itemObject.IsFood = obj.IsFood;
        itemObject.IsUniqueItem = obj.IsUniqueItem;
        itemObject.ScaleFactor = obj.ScaleFactor;

        // CultureObjectSurrogate 
        itemObject.Culture = obj.Culture;

        itemObject.MultiplayerItem = obj.MultiplayerItem;
        itemObject.NotMerchandise = obj.NotMerchandise;
        itemObject.IsCraftedByPlayer = obj.IsCraftedByPlayer;
        itemObject.LodAtlasIndex = obj.LodAtlasIndex;

        // WeaponDesignSurrogate
        itemObject.WeaponDesign = obj.WeaponDesign;

        itemObject.Type = (ItemObject.ItemTypeEnum)obj.Type;

        // Needs to be converted later
        itemObject.PrerequisiteItem = prequisteItem;

        // set a bunch of shit fields
        return itemObject;
    }
}

