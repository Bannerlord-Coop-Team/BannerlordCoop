using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class HeroDeveloperSerializer : CustomSerializer
    {
        public HeroDeveloperSerializer(HeroDeveloper value) : base(value) 
        {
            // TODO HeroDeveloperSerializer
        }

        public override object Deserialize()
        {
            throw new NotImplementedException();
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}