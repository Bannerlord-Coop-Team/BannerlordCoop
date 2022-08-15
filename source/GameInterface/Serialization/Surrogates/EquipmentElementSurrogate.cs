using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class EquipmentElementSurrogate
    {
        [ProtoMember(1)]
        bool IsQuestItem;
        [ProtoMember(2)]
        ItemObject Item;
        [ProtoMember(3)]
        ItemModifier ItemModifier;
        [ProtoMember(4)]
        ItemObject CosmeticItem;

        #region Reflection
        private static readonly PropertyInfo info_IsQuestItem = typeof(EquipmentElement).GetProperty(nameof(EquipmentElement.IsQuestItem), BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo info_Item = typeof(EquipmentElement).GetProperty(nameof(EquipmentElement.Item), BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo info_ItemModifier = typeof(EquipmentElement).GetProperty(nameof(EquipmentElement.ItemModifier), BindingFlags.Instance | BindingFlags.NonPublic);
        #endregion

        private EquipmentElementSurrogate(EquipmentElement obj)
        {
            IsQuestItem = obj.IsQuestItem;
            Item = obj.Item;
            ItemModifier = obj.ItemModifier;
            CosmeticItem = obj.CosmeticItem;
        }

        private EquipmentElement Deserialize()
        {
            EquipmentElement equipmentElement = default;
            info_IsQuestItem.SetValue(equipmentElement, IsQuestItem);
            info_Item.SetValue(equipmentElement, Item);
            info_ItemModifier.SetValue(equipmentElement, ItemModifier);
            equipmentElement.CosmeticItem = CosmeticItem;

            return equipmentElement;
        }

        public static implicit operator EquipmentElementSurrogate(EquipmentElement obj)
        {
            return new EquipmentElementSurrogate(obj);
        }

        public static implicit operator EquipmentElement(EquipmentElementSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
