using System;
using Coop.Mod.Serializers;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Serializers.CustomSerializers
{
    [Serializable]
    public class ItemDataSerializer : CustomSerializer
    {
        public ItemDataSerializer(ItemData itemData) : base(itemData)
        {
        }

        public override object Deserialize()
        {
            ItemData itemData = new ItemData();
            return base.Deserialize(itemData);
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}