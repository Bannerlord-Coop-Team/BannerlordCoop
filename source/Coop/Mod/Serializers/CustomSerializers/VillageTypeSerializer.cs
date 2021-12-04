using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class VillageTypeSerializer : CustomSerializer
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
            throw new NotImplementedException();
        }
    }
}
