using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class CultureObjectSerializer : CustomSerializer
    {
        string stringId;
        public CultureObjectSerializer(CultureObject culture) : base(culture) 
        {
            // TODO Find way to work better with other mods
            stringId = culture.StringId;
        }

        public override object Deserialize()
        {


            CultureObject cultureObject = MBObjectManager.Instance.GetObject<CultureObject>(stringId);
            return cultureObject;
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}