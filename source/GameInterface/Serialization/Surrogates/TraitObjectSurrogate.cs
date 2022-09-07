using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class TraitObjectSurrogate
    {
        #region Reflection
        private static readonly PropertyInfo p_MaxValue = typeof(TraitObject).GetProperty("MaxValue");
        private static readonly PropertyInfo p_MinValue = typeof(TraitObject).GetProperty("MinValue");
        private static readonly PropertyInfo p_IsHidden = typeof(TraitObject).GetProperty("IsHidden");

        private static readonly FieldInfo f_name = typeof(TraitObject).GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo f_description = typeof(TraitObject).GetField("_description", BindingFlags.NonPublic | BindingFlags.Instance);
        #endregion

        [ProtoMember(1)]
        private string StringId;

        public TraitObjectSurrogate(TraitObject obj)
        {
            StringId = obj.StringId;
        }

        public TraitObject Deserialize()
        {
            return MBObjectManager.Instance.GetObject<TraitObject>(StringId);
        }

        public static implicit operator TraitObjectSurrogate(TraitObject obj)
        {
            if (obj == null) return null;

            return new TraitObjectSurrogate(obj);
        }

        public static implicit operator TraitObject(TraitObjectSurrogate surrogate)
        {
            if (surrogate == null) return null;

            return surrogate.Deserialize();
        }
    }
}
