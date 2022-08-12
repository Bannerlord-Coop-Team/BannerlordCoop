using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Mod.Serializers.Custom
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