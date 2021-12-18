using System;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class VillageTypeSerializer : CustomSerializer
    {
        string StringId;
        public VillageTypeSerializer(MBObjectBase obj) : base(obj)
        {
            StringId = obj.StringId;
        }

        public override object Deserialize()
        {
            VillageType newVillagerType = new VillageType(StringId);
            return base.Deserialize(newVillagerType);
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}
