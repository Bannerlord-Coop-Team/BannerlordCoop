using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemModifierSurrogate
{
    [ProtoMember(1)]
    public int Armor { get; set; }
    [ProtoMember(2)]
    public float ChargeDamage { get; set; }
    [ProtoMember(3)]
    public int Damage { get; set; }
    [ProtoMember(4)]
    public short HitPoints { get; set; }
    [ProtoMember(5)]
    public ItemQuality ItemQuality { get; set; }
    [ProtoMember(6)]
    public float LootDropScore { get; set; }
    [ProtoMember(7)]
    public float Maneuver { get; set; }
    [ProtoMember(8)]
    public int MissileSpeed { get; set; }
    [ProtoMember(9)]
    public float MountHitPoints { get; set; }
    [ProtoMember(10)]
    public float MountSpeed { get; set; }
    [ProtoMember(11)]
    public TextObject Name { get; set; }
    [ProtoMember(12)]
    public float PriceMultiplier { get; set; }
    [ProtoMember(13)]
    public float ProductionDropScore { get; set; }
    [ProtoMember(14)]
    public int Speed { get; set; }
    [ProtoMember(15)]
    public short StackCount { get; set; }

    public ItemModifierSurrogate(ItemModifier itemModifier)
    {
        if(itemModifier == null)
        {
            Armor = -1;
            ChargeDamage = -1;
            Damage = -1;
            HitPoints = -1;
            ItemQuality = ItemQuality.Poor;
            LootDropScore = -1;
            Maneuver = -1;
            MissileSpeed = -1;
            MountHitPoints = -1;
            MountSpeed = -1;
            Name = new TextObject("Bad Name");
            PriceMultiplier = -1;
            ProductionDropScore = -1;
            Speed = -1;
            StackCount = -1;
        }
        else
        {
            Armor = itemModifier.Armor;
            ChargeDamage = itemModifier.ChargeDamage;
            Damage = itemModifier.Damage;
            HitPoints = itemModifier.HitPoints;
            ItemQuality = itemModifier.ItemQuality;
            LootDropScore = itemModifier.LootDropScore;
            Maneuver = itemModifier.Maneuver;
            MissileSpeed = itemModifier.MissileSpeed;
            MountHitPoints = itemModifier.MountHitPoints;
            MountSpeed = itemModifier.MountSpeed;
            Name = itemModifier.Name;
            PriceMultiplier = itemModifier.PriceMultiplier;
            ProductionDropScore = itemModifier.ProductionDropScore;
            Speed = itemModifier.Speed;
            StackCount = itemModifier.StackCount;
        }
    }


    public static implicit operator ItemModifierSurrogate(ItemModifier itemModifier)
    {
        return new ItemModifierSurrogate(itemModifier);
    }

    public static implicit operator ItemModifier(ItemModifierSurrogate surrogate)
    {
        ItemModifier itemModifier = new ItemModifier();
        
        itemModifier.Armor = surrogate.Armor;
        itemModifier.ChargeDamage = surrogate.ChargeDamage;
        itemModifier.Damage = surrogate.Damage;
        itemModifier.HitPoints = surrogate.HitPoints;
        itemModifier.ItemQuality = surrogate.ItemQuality;
        itemModifier.LootDropScore = surrogate.LootDropScore;
        itemModifier.Maneuver = surrogate.Maneuver;
        itemModifier.MissileSpeed = surrogate.MissileSpeed;
        itemModifier.MountHitPoints = surrogate.MountHitPoints;
        itemModifier.MountSpeed = surrogate.MountSpeed;
        itemModifier.Name = surrogate.Name;
        itemModifier.PriceMultiplier = surrogate.PriceMultiplier;
        itemModifier.ProductionDropScore = surrogate.ProductionDropScore;
        itemModifier.Speed = surrogate.Speed;
        itemModifier.StackCount = surrogate.StackCount;

        return itemModifier;
    }
}