using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class ItemObjectSerializer : CustomSerializer
    {
        string stringId;

        public ItemObjectSerializer(ItemObject item)
        {
            stringId = item.StringId;
        }

        public override object Deserialize()
        {
            return MBObjectManager.Instance.GetObject<ItemObject>(stringId);
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}