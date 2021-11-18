using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class CultureObjectSerializer : CustomSerializer
    {
        public CultureObjectSerializer(CultureObject culture) : base(culture) 
        {
            // TODO CultureObjectSerializer
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