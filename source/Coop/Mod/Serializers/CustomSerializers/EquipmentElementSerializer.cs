using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace MBMultiplayerCampaign.Serializers
{
    [Serializable]
    class EquipmentElementSerializer : ICustomSerializer
    {
        string itemModifier;
        uint id;

        public EquipmentElementSerializer() { }
        public EquipmentElementSerializer(EquipmentElement equipmentElement)
        {
            itemModifier = (equipmentElement.ItemModifier != null) ? equipmentElement.ItemModifier.ID : "";
            ItemObject item = equipmentElement.Item;
            id = (item != null) ? item.Id.InternalValue : 0U;
        }

        public ICustomSerializer Serialize(object obj)
        {
            return new EquipmentElementSerializer((EquipmentElement)obj);
        }

        object ICustomSerializer.Deserialize()
        {
            ItemModifier newItemModifier = null;
            if (itemModifier != "")
            {
                newItemModifier = Game.Current.ObjectManager.GetObject<ItemModifier>(itemModifier);
            }
            MBGUID objectId = new MBGUID(id);
            ItemObject newItem = (MBObjectManager.Instance.GetObject(objectId) as ItemObject);

            return new EquipmentElement(newItem, newItemModifier);
        }
    }
}
