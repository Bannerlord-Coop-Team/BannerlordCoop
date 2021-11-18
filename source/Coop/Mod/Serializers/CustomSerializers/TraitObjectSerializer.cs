using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class TraitObjectSerializer : CustomSerializer
    {
        string StringId;
        public TraitObjectSerializer(MBObjectBase obj) : base(obj)
        {
            StringId = obj.StringId;
        }

        public override object Deserialize()
        {

            TraitObject newTraitObject = new TraitObject(StringId);
            return base.Deserialize(newTraitObject);
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}
