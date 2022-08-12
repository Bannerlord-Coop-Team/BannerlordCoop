using System;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class EquipmentSerializer : ICustomSerializer
    {
        string equipmentCode;
        public EquipmentSerializer() { }
        public EquipmentSerializer(Equipment equipment)
        {
            equipmentCode = equipment.CalculateEquipmentCode();
        }

        public ICustomSerializer Serialize(object obj)
        {
            return new EquipmentSerializer((Equipment)obj);
        }
        public object Deserialize()
        {
            return Equipment.CreateFromEquipmentCode(equipmentCode);
        }

        public void ResolveReferenceGuids()
        {
            // No references
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
