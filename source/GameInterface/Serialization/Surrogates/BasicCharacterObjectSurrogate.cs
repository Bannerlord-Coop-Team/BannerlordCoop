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
        // ProtoMember(2)]
        // ...

        #region Reflection
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>
        {
            {nameof(_basicName), AccessTools.Field(typeof(BasicCharacterObject), nameof(_basicName)) },
            //...
        };

        #endregion

        public BasicCharacterObjectSurrogate(BasicCharacterObject basicCharacterObject)
        {
            _basicName = basicCharacterObject.Name;
            // TODO ...
        }

        private BasicCharacterObject Deserialize()
        {
            BasicCharacterObject newBasicCharacterObject = new BasicCharacterObject();

            Fields[nameof(_basicName)].SetValue(newBasicCharacterObject, _basicName);
            // TODO ...

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
