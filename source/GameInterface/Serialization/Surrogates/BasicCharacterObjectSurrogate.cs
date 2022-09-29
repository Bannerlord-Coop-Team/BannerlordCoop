using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public readonly struct BasicCharacterObjectSurrogate
    {
        [ProtoMember(1)]
        TextObject _basicName { get; }
        [ProtoMember(2)]
        bool _isMounted { get; }
        [ProtoMember(3)]
        bool _isRanged { get; }
        [ProtoMember(4)]
        MBEquipmentRoster _equipmentRoster { get; }
        [ProtoMember(5)]
        BasicCultureObject _culture { get; }
        [ProtoMember(6)]
        float _age { get; }
        [ProtoMember(7)]
        bool _isBasicHero { get; }
        [ProtoMember(8)]
        MBCharacterSkills CharacterSkills { get; }

        #region Reflection
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>
        {
            {nameof(_basicName), AccessTools.Field(typeof(BasicCharacterObject), nameof(_basicName)) },
            {nameof(_isMounted), AccessTools.Field(typeof(BasicCharacterObject), nameof(_isMounted)) },
            {nameof(_isRanged), AccessTools.Field(typeof(BasicCharacterObject), nameof(_isRanged)) },
            {nameof(_equipmentRoster), AccessTools.Field(typeof(BasicCharacterObject), nameof(_equipmentRoster)) },
            {nameof(_culture), AccessTools.Field(typeof(BasicCharacterObject), nameof(_culture)) },
            {nameof(_age), AccessTools.Field(typeof(BasicCharacterObject), nameof(_age)) },
            {nameof(_isBasicHero), AccessTools.Field(typeof(BasicCharacterObject), nameof(_isBasicHero)) },
            {nameof(CharacterSkills), AccessTools.Field(typeof(BasicCharacterObject), nameof(CharacterSkills)) },

    };

        #endregion

        public BasicCharacterObjectSurrogate(BasicCharacterObject basicCharacterObject)
        {
            _basicName = basicCharacterObject.Name;
            _isMounted = basicCharacterObject.IsMounted;
            _isRanged = basicCharacterObject.IsRanged;
            _equipmentRoster = (MBEquipmentRoster)AccessTools.Property(typeof(BasicCharacterObject), nameof(_equipmentRoster)).GetValue(basicCharacterObject);
            _culture = basicCharacterObject.Culture;
            _age = basicCharacterObject.Age;
            _isBasicHero = basicCharacterObject.IsHero;
            CharacterSkills = (MBCharacterSkills)AccessTools.Property(typeof(BasicCharacterObject), nameof(CharacterSkills)).GetValue(basicCharacterObject);

        }

        private BasicCharacterObject Deserialize()
        {
            BasicCharacterObject newBasicCharacterObject = new BasicCharacterObject();

            Fields[nameof(_basicName)].SetValue(newBasicCharacterObject, _basicName);
            Fields[nameof(_isMounted)].SetValue(newBasicCharacterObject, _isMounted);
            Fields[nameof(_isRanged)].SetValue(newBasicCharacterObject, _isRanged);
            Fields[nameof(_equipmentRoster)].SetValue(newBasicCharacterObject, _equipmentRoster);
            Fields[nameof(_culture)].SetValue(newBasicCharacterObject, _culture);
            Fields[nameof(_age)].SetValue(newBasicCharacterObject, _age);
            Fields[nameof(_isBasicHero)].SetValue(newBasicCharacterObject, _isBasicHero);
            Fields[nameof(CharacterSkills)].SetValue(newBasicCharacterObject, CharacterSkills);

            return newBasicCharacterObject;
        }

        /// <summary>
        ///     TODO
        /// </summary>
        /// <param name="basicCharacterObject">TODO</param>
        /// <returns>TODO</returns>
        public static implicit operator BasicCharacterObjectSurrogate(BasicCharacterObject basicCharacterObject)
        {
            return new BasicCharacterObjectSurrogate(basicCharacterObject);
        }

        /// <summary>
        ///     TODO
        /// </summary>
        /// <param name="surrogate">TODO</param>
        /// <returns>TODO</returns>
        public static implicit operator BasicCharacterObject(BasicCharacterObjectSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
