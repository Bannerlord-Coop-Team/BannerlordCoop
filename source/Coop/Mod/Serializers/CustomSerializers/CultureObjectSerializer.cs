using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class CultureObjectSerializer : ICustomSerializer
    {
        private string cultureId;
        public CultureObjectSerializer(CultureObject culture)
        {
            cultureId = culture.StringId;
        }

        public object Deserialize()
        {
            return MBObjectManager.Instance.GetObject(new MBGUID(uint.Parse(cultureId));
        }
    }
}