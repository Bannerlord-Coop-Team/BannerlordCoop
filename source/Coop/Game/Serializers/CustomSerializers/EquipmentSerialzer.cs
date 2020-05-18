using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace MBMultiplayerCampaign.Serializers
{
    [Serializable]
    class EquipmentSerializer : ICustomSerializer
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
    }
}
