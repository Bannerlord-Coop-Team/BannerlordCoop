using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates
{
    [ProtoContract]
    internal struct HorseComponentSurrogate
    {
        [ProtoMember(1)]
        public ItemObject Item { get; set; }
        [ProtoMember(2)]
        public ItemModifierGroup ItemModifierGroup { get; set; }
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


        public HorseComponentSurrogate(HorseComponent horseComponent)
        {
            if (horseComponent == null)
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
            }
            else
            {
                Item = horseComponent.Item;
                ItemModifierGroup = horseComponent.ItemModifierGroup;
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

        public static implicit operator HorseComponentSurrogate(HorseComponent horseComponent)
        {
            return new HorseComponentSurrogate(horseComponent);
        }

        public static implicit operator HorseComponent(HorseComponentSurrogate surrogate)
        {
            return new HorseComponent();
        }
    }
}
