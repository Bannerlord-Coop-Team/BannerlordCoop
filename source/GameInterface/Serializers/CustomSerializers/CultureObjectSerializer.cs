using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class CultureObjectSerializer : CustomSerializer
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
            // No references
        }
    }
}