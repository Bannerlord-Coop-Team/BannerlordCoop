using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ItemObjectSurrogate
{
    [ProtoIgnore]
    public ItemComponent ItemComponent { get; set; }
    [ProtoMember(2)]
    public string MultiMeshName { get; set; }
    [ProtoMember(3)]
    public string HolsterMeshName { get; set; }
    [ProtoMember(4)]
    public string HolsterWithWeaponMeshName { get; set; }
    [ProtoMember(5)]
    public string[] ItemHolsters { get; set; }
    [ProtoMember(6)]
    public Vec3 HolsterPositionShift { get; set; }
    [ProtoMember(7)]
    public bool HasLowerHolsterPriority { get; set; }
    [ProtoMember(8)]
    public string FlyingMeshName { get; set; }
    [ProtoMember(9)]
    public string BodyName { get; set; }
    [ProtoMember(10)]
    public string HolsterBodyName { get; set; }
    [ProtoMember(11)]
    public string CollisionBodyName { get; set; }
    [ProtoMember(12)]
    public bool RecalculateBody { get; set; }
    [ProtoMember(13)]
    public string PrefabName { get; set; }
    [ProtoMember(14)]
    public TextObject Name { get; set; }
    [ProtoMember(15)]
    public ItemFlags ItemFlags { get; set; }
    [ProtoMember(16)]
    public ItemCategory ItemCategory { get; set; }
    [ProtoMember(17)]
    public int Value { get; set; }
    [ProtoMember(18)]
    public float Effectiveness { get; set; }
    [ProtoMember(19)]
    public float Weight { get; set; }
    [ProtoMember(20)]
    public int Difficulty { get; set; }
    [ProtoMember(21)]
    public float Appearance { get; set; }
    [ProtoMember(22)]
    public bool IsUsingTableau { get; set; }

    public ItemObjectSurrogate(ItemObject ItemObject)
    {
        if (ItemObject == null)
        {
            ItemComponent = new TradeItemComponent();
            MultiMeshName = "";
            HolsterMeshName = "";
            HolsterWithWeaponMeshName = ""; 
            ItemHolsters = new string[] {""};
            HolsterPositionShift = ObjectHelper.SkipConstructor<Vec3>();
            HasLowerHolsterPriority = false;
            FlyingMeshName = "";
            BodyName = "";
            HolsterBodyName = "";
            CollisionBodyName = "";
            RecalculateBody = false;
            PrefabName = "";
            Name = ObjectHelper.SkipConstructor<TextObject>();
            ItemFlags = ItemFlags.Civilian;
            ItemCategory = ObjectHelper.SkipConstructor<ItemCategorySurrogate>();
            Value = -1;
            Effectiveness = -1f;
            Weight = -1f;
            Difficulty = -1;
            Appearance = -1f;
            IsUsingTableau = false; 
        }
        else
        {
            ItemComponent = ItemObject.ItemComponent;
            MultiMeshName = ItemObject.MultiMeshName;
            HolsterMeshName = ItemObject.HolsterMeshName;
            HolsterWithWeaponMeshName = ItemObject.HolsterWithWeaponMeshName; 
            ItemHolsters = ItemObject.ItemHolsters;
            HolsterPositionShift = ItemObject.HolsterPositionShift;
            HasLowerHolsterPriority = ItemObject.HasLowerHolsterPriority;
            FlyingMeshName = ItemObject.FlyingMeshName;
            BodyName = ItemObject.BodyName;
            HolsterBodyName = ItemObject.HolsterBodyName;
            CollisionBodyName = ItemObject.CollisionBodyName;
            RecalculateBody = ItemObject.RecalculateBody;
            PrefabName = ItemObject.PrefabName;
            Name = ItemObject.Name;
            ItemFlags = ItemObject.ItemFlags;
            ItemCategory = ItemObject.ItemCategory;
            Value = ItemObject.Value;
            Effectiveness = ItemObject.Effectiveness;
            Weight = ItemObject.Weight;
            Difficulty = ItemObject.Difficulty;
            Appearance = ItemObject.Appearance;
            IsUsingTableau = ItemObject.IsUsingTableau; 
        }
    }

    public static implicit operator ItemObjectSurrogate(ItemObject ItemObject)
    {
        return new ItemObjectSurrogate(ItemObject);
    }

    public static implicit operator ItemObject(ItemObjectSurrogate ItemObject)
    {
        return new ItemObject();
    }
}
