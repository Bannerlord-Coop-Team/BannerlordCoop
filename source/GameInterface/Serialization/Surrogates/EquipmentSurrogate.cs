using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Models
{
    [ProtoContract]
    public class EquipmentSurrogate
    {
        [ProtoMember(1)]
        Equipment.EquipmentType _equipmentType;
        [ProtoMember(2)]
        EquipmentElement[] _itemSlots;
        [ProtoMember(3)]
        bool SyncEquipments;

        #region Reflection
        private static readonly FieldInfo info_equipmentType = typeof(Equipment).GetField("_equipmentType", BindingFlags.NonPublic | BindingFlags.Instance);
        #endregion
        private EquipmentSurrogate(Equipment obj)
        {
            _equipmentType = (Equipment.EquipmentType)info_equipmentType.GetValue(obj);
            _itemSlots = new EquipmentElement[Equipment.EquipmentSlotLength];

            for (int i = 0; i < _itemSlots.Length; i++)
            {
                _itemSlots[i] = obj[i];
            }

            SyncEquipments = obj.SyncEquipments;
        }

        private Equipment Deserialize()
        {
            Equipment equipment = new Equipment();

            info_equipmentType.SetValue(equipment, _equipmentType);

            for (int i = 0; i < _itemSlots.Length; i++)
            {
                equipment[i] = _itemSlots[i];
            }

            equipment.SyncEquipments = SyncEquipments;

            return equipment;
        }

        public static implicit operator EquipmentSurrogate(Equipment obj)
        {
            return new EquipmentSurrogate(obj);
        }

        public static implicit operator Equipment(EquipmentSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
