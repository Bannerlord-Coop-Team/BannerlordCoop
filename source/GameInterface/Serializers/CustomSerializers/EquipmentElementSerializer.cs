using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class EquipmentElementSerializer : ICustomSerializer
    {
        string itemModifier;
        uint id;

        public EquipmentElementSerializer(EquipmentElement equipmentElement)
        {
            itemModifier = (equipmentElement.ItemModifier != null) ? equipmentElement.ItemModifier.Name.ToString() : "";
            ItemObject item = equipmentElement.Item;
            id = (item != null) ? item.Id.InternalValue : 0U;
        }

        public object Deserialize()
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
