using Common.Util;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Surrogates
{
    [ProtoContract]
    internal struct ArmorComponentSurrogate
    {
        [ProtoMember(1)]
        public ItemObject Item { get; set; }
        [ProtoMember(2)]
        public ItemModifierGroup ItemModifierGroup { get; set; }
        [ProtoMember(3)]
        public int HeadArmor { get; set; }
        [ProtoMember(4)]
        public int BodyArmor { get; set; }
        [ProtoMember(5)]
        public int LegArmor { get; set; }
        [ProtoMember(6)]
        public int ArmArmor { get; set; }
        [ProtoMember(7)]
        public int ManeuverBonus { get; set; }
        [ProtoMember(8)]
        public int SpeedBonus { get; set; }
        [ProtoMember(9)]
        public int ChargeBonus { get; set; }
        [ProtoMember(10)]
        public int FamilyType { get; set; }
        [ProtoMember(11)]
        public ArmorComponent.ArmorMaterialTypes MaterialType { get; set; }
        [ProtoMember(12)]
        public SkinMask MeshesMask { get; set; }
        [ProtoMember(13)]
        public ArmorComponent.BodyMeshTypes BodyMeshType { get; set; }
        [ProtoMember(14)]
        public ArmorComponent.BodyDeformTypes BodyDeformType { get; set; }
        [ProtoMember(15)]
        public ArmorComponent.HairCoverTypes HairCoverType { get; set; }
        [ProtoMember(16)]
        public ArmorComponent.BeardCoverTypes BeardCoverType { get; set; }
        [ProtoMember(17)]
        public ArmorComponent.HorseHarnessCoverTypes ManeCoverType { get; set; }
        [ProtoMember(18)]
        public ArmorComponent.HorseTailCoverTypes TailCoverType { get; set; }
        [ProtoMember(19)]
        public string ReinsMesh { get; set; }


        public ArmorComponentSurrogate(ArmorComponent armorComponent)
        {
            if (armorComponent == null)
            {
                Item = ObjectHelper.SkipConstructor<ItemObject>();
                ItemModifierGroup = ObjectHelper.SkipConstructor<ItemModifierGroup>();
                HeadArmor = -1;
                BodyArmor = -1;
                LegArmor = -1;
                ArmArmor = -1;
                ManeuverBonus = -1;
                SpeedBonus = -1;
                ChargeBonus = -1;
                FamilyType = -1;
                MaterialType = ArmorComponent.ArmorMaterialTypes.None;
                MeshesMask = ObjectHelper.SkipConstructor<SkinMask>();
                BodyMeshType = ArmorComponent.BodyMeshTypes.Normal;
                BodyDeformType = ArmorComponent.BodyDeformTypes.Medium;
                HairCoverType = ArmorComponent.HairCoverTypes.None;
                BeardCoverType = ArmorComponent.BeardCoverTypes.None;
                ManeCoverType = ArmorComponent.HorseHarnessCoverTypes.None;
                TailCoverType = ArmorComponent.HorseTailCoverTypes.None;
                ReinsMesh = "";

            }
            else
            {
                Item = armorComponent.Item;
                ItemModifierGroup = armorComponent.ItemModifierGroup;
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
        }

        public static implicit operator ArmorComponentSurrogate(ArmorComponent armorComponent)
        {
            return new ArmorComponentSurrogate(armorComponent);
        }

        public static implicit operator ArmorComponent(ArmorComponentSurrogate surrogate)
        {
            return new ArmorComponent(surrogate.Item);
        }
    }
}