using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class ItemModifierSurrogate
    {
        #region Fields
        [ProtoMember(1)]
        TextObject Name;
        [ProtoMember(2)]
        float PriceMultiplier;
        [ProtoMember(3)]
        int _armor;
        [ProtoMember(4)]
        float _chargeDamage;
        [ProtoMember(5)]
        int _damage;
        [ProtoMember(6)]
        short _hitPoints;
        [ProtoMember(7)]
        float _maneuver;
        [ProtoMember(8)]
        int _missleSpeed;
        [ProtoMember(9)]
        float _mountHitPoints;
        [ProtoMember(10)]
        float _mountSpeed;
        [ProtoMember(11)]
        int _speed;
        [ProtoMember(12)]
        short _stackCount;
        #endregion

        #region Reflection
        private static readonly PropertyInfo info_Name = SerializationHelper.GetPrivateProperty<ItemModifier>("Name");
        private static readonly PropertyInfo info_PriceMultiplier = SerializationHelper.GetPrivateProperty<ItemModifier>("PriceMultiplier");
        private static readonly FieldInfo info_armor = SerializationHelper.GetPrivateField<ItemModifier>("_armor");
        private static readonly FieldInfo info_chargeDamage = SerializationHelper.GetPrivateField<ItemModifier>("_chargeDamage");
        private static readonly FieldInfo info_damage = SerializationHelper.GetPrivateField<ItemModifier>("_damage");
        private static readonly FieldInfo info_hitPoints = SerializationHelper.GetPrivateField<ItemModifier>("_hitPoints");
        private static readonly FieldInfo info_maneuver = SerializationHelper.GetPrivateField<ItemModifier>("_maneuver");
        private static readonly FieldInfo info_missleSpeed = SerializationHelper.GetPrivateField<ItemModifier>("_missleSpeed");
        private static readonly FieldInfo info_mountHitPoints = SerializationHelper.GetPrivateField<ItemModifier>("_mountHitPoints");
        private static readonly FieldInfo info_mountSpeed = SerializationHelper.GetPrivateField<ItemModifier>("_mountSpeed");
        private static readonly FieldInfo info_speed =      SerializationHelper.GetPrivateField<ItemModifier>("_speed");
        private static readonly FieldInfo info_stackCount = SerializationHelper.GetPrivateField<ItemModifier>("_stackCount");
        #endregion

        private ItemModifierSurrogate(ItemModifier obj)
        {
            Name = obj.Name;
            PriceMultiplier = obj.PriceMultiplier;
            SerializationHelper.AssignAsObjectType(ref _armor,          info_armor, obj);
            SerializationHelper.AssignAsObjectType(ref _chargeDamage,   info_chargeDamage, obj);
            SerializationHelper.AssignAsObjectType(ref _damage,         info_damage, obj);
            SerializationHelper.AssignAsObjectType(ref _hitPoints,      info_hitPoints, obj);
            SerializationHelper.AssignAsObjectType(ref _maneuver,       info_maneuver, obj);
            SerializationHelper.AssignAsObjectType(ref _missleSpeed,    info_missleSpeed, obj);
            SerializationHelper.AssignAsObjectType(ref _mountHitPoints, info_mountHitPoints, obj);
            SerializationHelper.AssignAsObjectType(ref _mountSpeed,     info_mountSpeed, obj);
            SerializationHelper.AssignAsObjectType(ref _stackCount,     info_speed, obj);

        }

        private ItemModifier Deserialize()
        {
            ItemModifier modifier = new ItemModifier();

            info_Name.SetValue(modifier, Name);
            info_PriceMultiplier.SetValue(modifier, PriceMultiplier);
            info_armor.SetValue(modifier, _armor);
            info_chargeDamage.SetValue(modifier, _chargeDamage);
            info_damage.SetValue(modifier, _damage);
            info_maneuver.SetValue(modifier, _maneuver);
            info_missleSpeed.SetValue(modifier, _missleSpeed);
            info_mountHitPoints.SetValue(modifier, _mountHitPoints);
            info_mountSpeed.SetValue(modifier, _mountSpeed);
            info_stackCount.SetValue(modifier, _stackCount);

            return modifier;
        }

        public static implicit operator ItemModifierSurrogate(ItemModifier obj)
        {
            if(obj == null) return null;
            return new ItemModifierSurrogate(obj);
        }

        public static implicit operator ItemModifier(ItemModifierSurrogate surrogate)
        {
            if(surrogate == null) return null;
            return surrogate.Deserialize();
        }
    }
}
