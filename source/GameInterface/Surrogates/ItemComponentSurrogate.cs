using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.Core;

namespace GameInterface.Surrogates;
[ProtoContract]
internal struct ItemComponentSurrogate
{
    [ProtoMember(1)]
    public ItemObjectSurrogate Item { get; set; }
    [ProtoMember(2)]
    public ItemModifierGroup ItemModifierGroup { get; set; }

    //Horse
    [ProtoMember(3)]
    public Monster Monster { get; set; }
    [ProtoMember(4)]
    public int Manuever { get; set; }
    [ProtoMember(5)]
    public int ChargeDamage { get; set; }
    [ProtoMember(6)]
    public int Speed { get; set; }
    [ProtoMember(7)]
    public int BodyLength { get; set; }
    [ProtoMember(8)]
    public int HitPoints { get; set; }
    [ProtoMember(9)]
    public bool IsRideable { get; set; }
    [ProtoMember(10)]
    public bool IsPackAnimal { get; set; }
    [ProtoMember(11)]
    public SkeletonScale SkeletonScale { get; set; }

    //Armor
    [ProtoMember(12)]
    public int HeadArmor { get; set; }
    [ProtoMember(13)]
    public int BodyArmor { get; set; }
    [ProtoMember(14)]
    public int LegArmor { get; set; }
    [ProtoMember(15)]
    public int ArmArmor { get; set; }
    [ProtoMember(16)]
    public int ManeuverBonus { get; set; }
    [ProtoMember(17)]
    public int SpeedBonus { get; set; }
    [ProtoMember(18)]
    public int ChargeBonus { get; set; }
    [ProtoMember(19)]
    public int FamilyType { get; set; }
    [ProtoMember(20)]
    public ArmorComponent.ArmorMaterialTypes MaterialType { get; set; }
    [ProtoMember(21)]
    public SkinMask MeshesMask { get; set; }
    [ProtoMember(22)]
    public ArmorComponent.BodyMeshTypes BodyMeshType { get; set; }
    [ProtoMember(23)]
    public ArmorComponent.BodyDeformTypes BodyDeformType { get; set; }
    [ProtoMember(24)]
    public ArmorComponent.HairCoverTypes HairCoverType { get; set; }
    [ProtoMember(25)]
    public ArmorComponent.BeardCoverTypes BeardCoverType { get; set; }
    [ProtoMember(26)]
    public ArmorComponent.HorseHarnessCoverTypes ManeCoverType { get; set; }
    [ProtoMember(27)]
    public ArmorComponent.HorseTailCoverTypes TailCoverType { get; set; }
    [ProtoMember(28)]
    public string ReinsMesh { get; set; }
    [ProtoMember(29)]
    public ItemComponentType ComponentType { get; set; }
    
    public enum ItemComponentType
    {
        None,
        Weapon,
        TradeItem,
        Saddle,
        Horse,
        Armor
    }

    public ItemComponentSurrogate(ItemComponent itemComponent)
    {
        Item = ObjectHelper.SkipConstructor<ItemObject>();
        ItemModifierGroup = ObjectHelper.SkipConstructor<ItemModifierGroup>();
        Monster = ObjectHelper.SkipConstructor<Monster>();
        Manuever = -1;
        ChargeDamage = -1;
        Speed = -1;
        BodyLength = -1;
        HitPoints = -1;
        IsRideable = false;
        IsPackAnimal = false;
        SkeletonScale = ObjectHelper.SkipConstructor<SkeletonScale>();
        HeadArmor = -1;
        BodyArmor = -1;
        LegArmor = -1;
        ArmArmor = -1;
        ManeuverBonus = -1;
        SpeedBonus = -1;
        ChargeBonus = -1;
        FamilyType = -1;
        MaterialType = ArmorComponent.ArmorMaterialTypes.None;
        MeshesMask = SkinMask.NoneVisible;
        BodyMeshType = ArmorComponent.BodyMeshTypes.Normal;
        BodyDeformType = ArmorComponent.BodyDeformTypes.Medium;
        HairCoverType = ArmorComponent.HairCoverTypes.None;
        BeardCoverType = ArmorComponent.BeardCoverTypes.None;
        ManeCoverType = ArmorComponent.HorseHarnessCoverTypes.None;
        TailCoverType = ArmorComponent.HorseTailCoverTypes.None;
        ReinsMesh = "";
        ComponentType = ItemComponentType.None;

        if (itemComponent != null)
        {
            Item = itemComponent.Item;
            ItemModifierGroup = itemComponent.ItemModifierGroup;

            if (itemComponent is ArmorComponent armorComponent)
            {
                HeadArmor = armorComponent.HeadArmor;
                BodyArmor = armorComponent.BodyArmor;
                LegArmor = armorComponent.LegArmor;
                ArmArmor = armorComponent.ArmArmor;
                ManeuverBonus = armorComponent.ManeuverBonus;
                SpeedBonus = armorComponent.SpeedBonus;
                ChargeBonus = armorComponent.ChargeBonus;
                FamilyType = armorComponent.FamilyType;
                MaterialType = armorComponent.MaterialType;
                MeshesMask = armorComponent.MeshesMask;
                BodyMeshType = armorComponent.BodyMeshType;
                BodyDeformType = armorComponent.BodyDeformType;
                HairCoverType = armorComponent.HairCoverType;
                BeardCoverType = armorComponent.BeardCoverType;
                ManeCoverType = armorComponent.ManeCoverType;
                TailCoverType = armorComponent.TailCoverType;
                ReinsMesh = armorComponent.ReinsMesh;
            }
            else if (itemComponent is HorseComponent horseComponent)
            {
                Monster = horseComponent.Monster;
                Manuever = horseComponent.Maneuver;
                ChargeDamage = horseComponent.ChargeDamage;
                Speed = horseComponent.Speed;
                BodyLength = horseComponent.BodyLength;
                HitPoints = horseComponent.HitPoints;
                IsRideable = horseComponent.IsRideable;
                IsPackAnimal = horseComponent.IsPackAnimal;
                SkeletonScale = horseComponent.SkeletonScale;
            }
        }
        if (itemComponent != null)
        {
            Type type = itemComponent.GetType();

            if (type == typeof(ArmorComponent))
                ComponentType = ItemComponentType.Armor;
            else if (type == typeof(HorseComponent))
                ComponentType = ItemComponentType.Horse;
            else if (type == typeof(WeaponComponent))
                ComponentType = ItemComponentType.Weapon;
            else if (type == typeof(TradeItemComponent))
                ComponentType = ItemComponentType.TradeItem;
            else if (type == typeof(SaddleComponent))
                ComponentType = ItemComponentType.Saddle;
            else
                ComponentType = ItemComponentType.None;
        }
    }

    public static implicit operator ItemComponentSurrogate(ItemComponent itemComponent)
    {
        return new ItemComponentSurrogate(itemComponent);
    }

    public static implicit operator ItemComponent(ItemComponentSurrogate surrogate)
    {
        switch (surrogate.ComponentType)
        {
            case ItemComponentType.Weapon:
                return new WeaponComponent(surrogate.Item);
            case ItemComponentType.TradeItem:
                return new TradeItemComponent();
            case ItemComponentType.Saddle:
                return new SaddleComponent((SaddleComponent)surrogate);
            case ItemComponentType.Horse:
                return new HorseComponent();
            case ItemComponentType.Armor:
                return new ArmorComponent(surrogate.Item);
            default:
                throw new InvalidOperationException("Unknown ItemComponentSurrogate type.");
        }
    }
}

