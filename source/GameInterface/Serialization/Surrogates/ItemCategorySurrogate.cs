using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class ItemCategorySurrogate
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

        private ItemCategorySurrogate(ItemCategory obj)
        {
            StringId = obj.StringId;
        }

        private ItemCategory Deserialize()
        {
            return MBObjectManager.Instance.GetObject<ItemCategory>(StringId);
        }

        public static implicit operator ItemCategorySurrogate(ItemCategory obj)
        {
            if (obj == null) return null;

            return new ItemCategorySurrogate(obj);
        }

        public static implicit operator ItemCategory(ItemCategorySurrogate surrogate)
        {
            if (surrogate == null) return null;

            return surrogate.Deserialize();
        }
    }
}
